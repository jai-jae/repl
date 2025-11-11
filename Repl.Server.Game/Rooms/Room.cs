using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Repl.Server.Core.Logging;
using Repl.Server.Core.Network;
using Repl.Server.Game.Entities;
using Repl.Server.Game.Entities.Components;
using Repl.Server.Game.Managers.Rooms;
using Repl.Server.Game.Network;
using Repl.Server.Game.Rooms.RoomState;
using static ReplGameProtocol.GS2CProtocol.Types;
using Vector2 = Repl.Server.Core.MathUtils.Vector2;

namespace Repl.Server.Game.Rooms;

public enum InteractionType
{
    Collision,
    PickupAttempt,
    Drop,
    Drill
}

public class GameRoom : ITickable, IDisposable
{
    private ILogger<GameRoom> logger = Log.CreateLogger<GameRoom>();
    
    private static int globalIdCounter = 0;
    public static int NextGlobalId() => Interlocked.Increment(ref globalIdCounter);
    private int localIdCounter = 0;
    private int GenerateEntityId() => Interlocked.Increment(ref localIdCounter);
    
    private bool initialized = false;
    
    private readonly ConcurrentDictionary<long, Player> players = new();
    private readonly ConcurrentDictionary<long, long> playerEntityIdToClientId = new();
    
    private readonly Dictionary<long, ResourceEntity> resources = new();
    private readonly ConcurrentQueue<ClientTransformMessage> transformUpdates = new();
    private readonly ConcurrentQueue<ClientInteractionMessage> interactionMessages = new();

    private readonly List<EntitySnapshot> pendingSnapshots = new();
    private readonly List<IServerEvent> pendingReliableEvents = new();

    private bool disposed = false;
    
    public long Id { get; }
    public long Seed { get; } // deterministic randomness on clients
    public TickRate RequiredTickRate { get; } = TickRate.High;
    public long CurrentTick { get; private set; }
    private readonly INetProtocol netProtocol;
    
    public DateTime? RoomEmptySince { get; set; }
    public bool Disposed => this.disposed;
    
    public GameRoom(INetProtocol protocol, int seed)
    {
        this.Id = 1;
        this.Seed = seed;
        this.netProtocol = protocol;
    }

    public bool ShouldTick()
    {
        return true;
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
        }

        this.disposed = true;   
    }
    
    public bool PlayerJoin(ReplGameSession session)
    {
        long clientId = session.ClientId;
        var entity = SpawnPlayerEntity(clientId, Vector2.Zero);
        var newPlayer = new Player(session, entity);
        
        if (this.players.TryAdd(clientId, newPlayer))
        {
            if (this.playerEntityIdToClientId.TryAdd(entity.Id, clientId) == false)
            {
                this.players.TryRemove(clientId, out _);
                logger.LogError($"Failed to add player to entity index (EntityId: {entity.Id}), state rolled back.");
                session.Dispose();
                return false;
            }
        }
        else
        {
            logger.LogWarning($"Player with ClientId {clientId} tried to join but already exists.");
            session.Dispose();
            return false;
        }

        return true;
    }
    
    public void PlayerLeave(long clientId)
    {
        if (this.players.TryRemove(clientId, out var leavingPlayer))
        {
            int entityId = leavingPlayer.Entity.Id;
            this.playerEntityIdToClientId.TryRemove(entityId, out _);

            // TODO: more cleanups
        }
    }
    
    public void QueueEntityTransformUpdate(long clientId, long entityId, Vector2 position, Vector2 velocity,
        float rotation)
    {
        var update = new ClientTransformMessage()
        {
            EntityId = entityId,
            ClientId = clientId,
            Timestamp = this.CurrentTick,
            Position = position,
            Velocity = velocity,
            Rotation = rotation
        };
        this.transformUpdates.Enqueue(update);
    }

    public void QueueInteractionCommand(long clientId, long playerEntityId, long targetEntityId, InteractionType type, Vector2? dropVelocity = null)
    {
        this.interactionMessages.Enqueue(new ClientInteractionMessage()
        {
            ClientId = clientId,
            PlayerEntityId = playerEntityId,
            TargetEntityId = targetEntityId,
            Type = type,
            DropVelocity = dropVelocity,
            Timestamp = this.CurrentTick,
        });
    }
    
    private void AddEntity(ResourceEntity entity, long currentTick)
    {
        resources.Add(entity.Id, entity);

        pendingReliableEvents.Add(new EntitySpawnedEvent
        {
            Tick = currentTick,
            EntityId = entity.Id,
            EntityType = entity.Type,
            Position = entity.GetComponent<TransformComponent>()?.Position ?? Vector2.Zero,
            ResourceTypeId = entity.ResourceTypeId
        });
    }
    
    private void AddEntity(PlayerEntity player, long currentTick)
    {
        pendingReliableEvents.Add(new EntitySpawnedEvent
        {
            Tick = currentTick,
            EntityId = player.Id,
            EntityType = player.Type,
            Position = player.GetComponent<TransformComponent>()?.Position ?? Vector2.Zero,
            ResourceTypeId = null
        });
    }

    public PlayerEntity SpawnPlayerEntity(long clientId, Vector2 spawnPosition)
    {
        var player = new PlayerEntity(
            id: GenerateEntityId(),
            currentTime: (float)CurrentTick,
            clientId: clientId,
            spawnPosition: spawnPosition
        );
        
        AddEntity(player, this.CurrentTick);
        return player;
    }

    public bool TryGetResourceEntity(long entityId, [NotNullWhen(true)] out ResourceEntity? entity)
    {
        if (this.resources.TryGetValue(entityId, out var resource) == false)
        {
            entity = null;
            return false;
        }
        entity = resource;
        return true;
    }
    
    public bool TryGetPlayerEntity(long entityId, [NotNullWhen(true)] out PlayerEntity? entity)
    {
        entity = null;
        if (this.TryGetPlayerByEntityId(entityId, out Player? player))
        {
            entity = player.Entity;
            return true;
        }
        return false;
    }
    
    public bool TryGetPlayerByEntityId(long entityId, [NotNullWhen(true)] out Player? player)
    {
        player = null;
        if (this.playerEntityIdToClientId.TryGetValue(entityId, out var clientId))
        {
            if (this.players.TryGetValue(clientId, out player) == false)
            {
                return false;
            }
            return true;   
        }
        return false;
    }
    
    public void Tick(TickContext context)
    {
        var currentTick = context.TickNumber;
        
        // Step 1: Process all queued transform updates from clients
        this.ProcessClientTransformUpdates(currentTick);
        
        // Step 2: Process interaction messages (collision/pickup/drop/actions)
        this.ProcessClientInteractionMessages(currentTick);
        
        // Step 3: Update all entities and their components
        this.UpdateAllEntities(currentTick);

        // Step 3: Update the core game logic and simulation.
        // Spawn + MarkForDelete + Apply Damage + Destruction etc. here
        this.UpdateGameLogic(currentTick);
        
        // Step 6: Generate reliable events snapshot for the current tick.
        var eventsSnapshot = GenerateReliableEventSnapshot(currentTick);

        // Step 7: Generate entity state snapshots for all entities
        var entitySnapshot = GenerateEntitySnapshot(currentTick);
        
        // Step 6: Broadcast all updates to clients
        this.BroadcastUpdatesToClients(currentTick, eventsSnapshot, entitySnapshot);
        
        // Step 7: Clean up destroyed entities
        this.CleanupDestroyedEntities(currentTick);
    }
    
    private void ProcessClientTransformUpdates(float currentTime)
    {
        while (transformUpdates.TryDequeue(out var update))
        {
            // Validate client owns the entity they're updating
            if (!ValidateClientOwnsEntity(update.ClientId, update.EntityId))
                continue;
                
            // Find the entity and update its transform
            var entity = GetEntity(update.EntityId);
            if (entity == null)
            {
                continue;
            }
            
            var transform = entity.GetComponent<TransformComponent>();
            if (transform == null)
            {
                continue;
            }
            
            // Validate and apply transform update
            if (transform.UpdateFromClient(update.Position, update.Rotation, update.Timestamp))
            {
                // Update velocity for physics continuation if needed
                if (entity is ResourceEntity resourceEntity)
                {
                    // Store velocity for potential ownership transfers
                    StoreEntityTransform(resourceEntity.Id, update.Velocity);
                }
            }
        }
    }
    
    private void ProcessClientInteractionMessages(long currentTick)
    {
        while (interactionMessages.TryDequeue(out var message))
        {
            this.TryGetPlayerEntity(message.PlayerEntityId, out var playerEntity);
            if (playerEntity == null) continue;
            
            switch (message.Type)
            {
                case InteractionType.Collision:
                    ProcessCollisionInteraction(playerEntity, message.TargetEntityId, currentTick);
                    break;
                    
                case InteractionType.PickupAttempt:
                    ProcessPickupInteraction(playerEntity, message.TargetEntityId, currentTick);
                    break;
                    
                case InteractionType.Drop:
                    ProcessDropInteraction(playerEntity, message.DropVelocity, currentTick);
                    break;
                case InteractionType.Drill:
                    // ProcessDrillInteraction(damageMessage);
                    break;
            }
        }
    }
    
    private void ProcessCollisionInteraction(PlayerEntity player, long targetEntityId, long currentTick)
    {
        if (!this.resources.TryGetValue(targetEntityId, out var resourceEntity))
        {
            return;
        }

        var ownership = resourceEntity.GetComponent<NetworkOwnershipComponent>();
        if (ownership == null)
        {
            return;
        }

        var result = ownership.RequestOwnership(player.ClientId, OwnershipPriority.Collision, currentTick);

        if (result == OwnershipResult.Granted)
        {
            this.pendingReliableEvents.Add(new OwnershipChangedEvent
            {
                Tick = currentTick,
                EntityId = resourceEntity.Id,
                NewOwnerClientId = player.ClientId,
                NewPriority = OwnershipPriority.Collision
            });
            Console.WriteLine($"Tick {currentTick}: Ownership of {resourceEntity.Id} GRANTED to client {player.ClientId} via Collision.");
        }
    }
    
    private void ProcessDropInteraction(PlayerEntity playerEntity, Vector2? dropVelocity, long currentTick)
    {
        // 1. gather entities + components
        if (!playerEntity.TryGetComponent<CarrierComponent>(out var carrier) || !carrier.IsCarrying)
        {
            return;
        }
        
        // 2. apply change
        if (carrier.TryClearCarriedItem(out var droppedEntityId, out var droppedWeight))
        {
            if (resources.TryGetValue(droppedEntityId.Value, out var droppedResource) &&
                droppedResource.TryGetComponent<CarryableComponent>(out var carryable) &&
                droppedResource.TryGetComponent<NetworkOwnershipComponent>(out var ownership))
            {
                carryable.Drop(dropVelocity);
                ownership.ResetPriorityToDefault(currentTick);

                // 3. generate events to publish
                this.pendingReliableEvents.Add(new CarryStateChangedEvent
                {
                    Tick = currentTick,
                    CarrierEntityId = playerEntity.Id,
                    CarryableEntityId = droppedEntityId.Value,
                    IsCarried = false,
                    DropVelocity = dropVelocity
                });

                this.pendingReliableEvents.Add(new OwnershipChangedEvent
                {
                    Tick = currentTick,
                    EntityId = droppedResource.Id,
                    NewOwnerClientId = ownership.OwnerClientId, // Owner doesn't change
                    NewPriority = OwnershipPriority.Default
                });
                Console.WriteLine($"Tick {currentTick}: Player {playerEntity.Id} DROPPED Resource {droppedEntityId.Value}. Ownership priority reset to Default.");
            }
        }
    }
    
    private void ProcessPickupInteraction(PlayerEntity playerEntity, long targetEntityId, long currentTick)
{
    // 1. gather entities + components
    if (this.resources.TryGetValue(targetEntityId, out var resourceEntity) == false ||
        playerEntity.TryGetComponent<CarrierComponent>(out var carrier) == false ||
        playerEntity.TryGetComponent<TransformComponent>(out var playerTransform) == false ||
        resourceEntity.TryGetComponent<CarryableComponent>(out var carryable) == false ||
        resourceEntity.TryGetComponent<NetworkOwnershipComponent>(out var ownership) == false ||
        resourceEntity.TryGetComponent<TransformComponent>(out var resourceTransform) == false)
    {
        return;
    }

    if (resourceEntity.IsMarkedForDeletion == true)
    {
        return;
    }
    
    // 2. vavlidation
    if (carryable.IsBeingCarried)
    {
        return;
    }

    if (carrier.CurrentCarryWeight + carryable.Weight > carrier.MaxCarryCapacity)
    {
        return;
    }
    var distance = Vector2.Distance(playerTransform.Position, resourceTransform.Position);
    if (distance > carrier.PickupRange)
    {
        return;
    }
    
    // 3. apply change
    carrier.AssignCarriedItem(resourceEntity.Id, carryable.Weight);
    carryable.PickUp(playerEntity.Id);
    ownership.ForceInteractionOwnership(playerEntity.ClientId, currentTick);

    // 4. generate events to publish
    this.pendingReliableEvents.Add(new CarryStateChangedEvent
    {
        Tick = currentTick,
        CarrierEntityId = playerEntity.Id,
        CarryableEntityId = resourceEntity.Id,
        IsCarried = true
    });
    this.pendingReliableEvents.Add(new OwnershipChangedEvent
    {
        Tick = currentTick,
        EntityId = resourceEntity.Id,
        NewOwnerClientId = playerEntity.ClientId,
        NewPriority = OwnershipPriority.Interaction
    });

    this.logger.LogDebug($"Tick {currentTick}: Player {playerEntity.Id} PICKED UP Resource {resourceEntity.Id}. Ownership forced to Interaction.");
}
    
    private void UpdateAllEntities(float deltaTime)
    {
        foreach (var player in players.Values)
        {
            if (!player.Entity.IsMarkedForDeletion)
            {
                player.Entity.Update(deltaTime);
            }
        }
        
        foreach (var resource in resources.Values)
        {
            if (!resource.IsMarkedForDeletion)
            {
                resource.Update(deltaTime);
            }
        }
    }

    private void UpdateGameLogic(long currentTick)
    {
        // core game state logic
        // destroy / spawn 
        // apply state change through queued interactions
    }
    
    private Packet.Types.GameplayEventUpdate GenerateReliableEventSnapshot(long currentTick)
    {
        var snapshot = new List<IServerEvent>(pendingReliableEvents);
        return new Packet.Types.GameplayEventUpdate()
        {
            ServerTick = currentTick,
            // TODO serialization with this.pendingReliableEvents
        };
    }
    
    private Packet.Types.EntitySnapshotUpdate GenerateEntitySnapshot(long currentTick)
    {
        // TODO: serialization
        return new Packet.Types.EntitySnapshotUpdate()
        {
            ServerTick = currentTick,
            // TODO serialization with this.pendingSnapshots
        };
    }
    
    private void BroadcastUpdatesToClients(long currentTick, Packet.Types.GameplayEventUpdate reliableEvents, Packet.Types.EntitySnapshotUpdate snapshots)
    {
        if (reliableEvents.Events.Count > 0)
        {
            NetworkUtility.Broadcast(
                sourceItems: this.players.Values,
                sessionSelector: p => p.Session,
                reliableEvents,
                this.netProtocol,
                this.logger);
        }
        
        if (snapshots.Entities.Count > 0)
        {
            NetworkUtility.Broadcast(
                sourceItems: this.players.Values,
                sessionSelector: p => p.Session,
                snapshots,
                this.netProtocol,
                this.logger);
        }
        
        // TODO: pendingLists should be cached within ClienSession for Re-Send mechanics
    }

    private void CleanupDestroyedEntities(long currentTick)
    {
        // Clean up players
        var playersToRemove = this.players.Values.Where(p => p.Entity.IsMarkedForDeletion).ToList();
        foreach (var player in playersToRemove)
        {
            if (player.Entity.TryGetComponent<CarrierComponent>(out var carrier) && carrier.IsCarrying)
            {
                var droppedItems = carrier.ForceClearAllCarriedItems();
                foreach (var item in droppedItems)
                {
                    var droppedEntityId = item.Key;
                    
                    if (this.resources.TryGetValue(droppedEntityId, out var droppedResource) &&
                        droppedResource.TryGetComponent<CarryableComponent>(out var carryable) &&
                        droppedResource.TryGetComponent<NetworkOwnershipComponent>(out var ownership))
                    {
                        carryable.Drop(Vector2.Zero);
                        ownership.ResetPriorityToDefault(currentTick);
                        
                        this.pendingReliableEvents.Add(new CarryStateChangedEvent
                        {
                            Tick = currentTick,
                            CarrierEntityId = player.Entity.Id,
                            CarryableEntityId = droppedResource.Id,
                            IsCarried = false,
                            DropVelocity = Vector2.Zero
                        });
                        
                        this.pendingReliableEvents.Add(new OwnershipChangedEvent
                        {
                            Tick = currentTick,
                            EntityId = droppedResource.Id,
                            NewOwnerClientId = ownership.OwnerClientId,
                            NewPriority = OwnershipPriority.Default
                        });
                        
                        this.logger.LogDebug($"Tick {currentTick}: Player {player.ClientId} disconnected, forcing drop of {droppedResource.Id}.");
                    }
                }
            }
        }
        
        // Clean up resources
        var resourcesToRemove = this.resources.Values.Where(r => r.IsMarkedForDeletion).ToList();
        foreach (var resource in resourcesToRemove)
        {
            this.resources.Remove(resource.Id);
            resource.Dispose();
        }
    }
    
    private PlayerEntity? FindClosestPlayer(ResourceEntity resource)
    {
        var resourceTransform = resource.GetComponent<TransformComponent>();
        if (resourceTransform == null)
        {
            return null;
        }
        
        PlayerEntity? closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (var player in players.Values)
        {
            var playerTransform = player.Entity.GetComponent<TransformComponent>();
            if (playerTransform == null) continue;
            
            var distance = Vector2.Distance(resourceTransform.Position, playerTransform.Position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player.Entity;
            }
        }
        
        return closest;
    }
    
    public bool ValidateClientOwnsEntity(long clientId, long entityId)
    {
        var entity = this.GetEntity(entityId);
        if (entity == null)
        {
            return false;
        }
        
        // Players can always update their own entity
        if (entity is PlayerEntity player)
        {
            return player.ClientId == clientId;
        }
            
        // For resources, check network ownership
        if (entity is ResourceEntity resource)
        {
            var ownership = resource.GetComponent<NetworkOwnershipComponent>();
            return ownership?.OwnerClientId == clientId;
        }
        
        return false;
    }
    
    private Entity? GetEntity(long entityId)
    {
        if (this.TryGetPlayerEntity(entityId, out var player))
        {
            return player;
        }

        if (this.resources.TryGetValue(entityId, out var resource))
        {
            return resource;
        }
            
        return null;
    }
    
    private void StoreEntityTransform(long entityId, Vector2 velocity) => throw new NotImplementedException();
}
