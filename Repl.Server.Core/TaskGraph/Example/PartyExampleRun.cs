using System.Diagnostics;
using Repl.Server.Core.TaskGraph.ResourceManagement;
using Repl.Server.Core.TaskGraph.TaskNode;
namespace Repl.Server.Core.TaskGraph.Example;

public static class PartyExampleRun
{
    public static async Task Run()
    {
        var graphResourceManager = new SharedResourceManager();
        var partyManager = new PartyManager();
        var taskGraphBuilder = new TaskGraphBuilder();
        
        
        long partyId = 101;
        var myParty = partyManager.GetOrCreate(partyId);
        var playersTryingToJoin = new[] { 10, 11, 12, 13, 14, 15 };

        Console.WriteLine($"Party Example Test Run\n");
        
        foreach (var player in playersTryingToJoin)
        {
            taskGraphBuilder.AddTask(
                taskId: $"JoinParty_{player}",
                requiredResources: [
                    graphResourceManager.GetOrCreateResource(ResourceTagFactory.ForPartyMembers(partyId))
                ],
                work: async _ => {
                    Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] -> Joining party. player: {player}");
                    await Task.Delay(50);
                    if (player == 59) throw new Exception("Hello World.");
                    if (myParty.AddPartyMember(player) == false)
                    {
                        Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] <- Party is full. player: {player}");
                        return WorkResult.Fail(new Error($"party is full"));
                    }
                    Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] <- Party joined. player: {player}");
                    return WorkResult.Ok();
                });
        }

        taskGraphBuilder.AddTask(
            taskId: "LeaveParty",
            requiredResources: [
                graphResourceManager.GetOrCreateResource(ResourceTagFactory.ForPartyMembers(partyId))
            ],
            async _ =>
            {
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] -> leave party. player: {14}");
                await Task.Delay(5000);
                myParty.LeaveParty(14);
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] <- Party left. player: {14}");
                return WorkResult.Ok();
            }, 1);
        
        taskGraphBuilder.AddTask(
            taskId: $"ChangePartyName",
            requiredResources: new[] { graphResourceManager.GetOrCreateResource(ResourceTagFactory.ForPartyName(partyId)) },
            work: async _ => {
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] -> Changing party name...");
                await Task.Delay(200);
                myParty.Name = "John Wick";
                Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}][{Stopwatch.GetTimestamp()}] <- Party name changed.");
                return WorkResult.Ok();
            });

        var graph = taskGraphBuilder.Build();
        var executor = new TaskGraphExecutor(4);
        await executor.ExecuteAsync(graph);
        
        Console.WriteLine($"\nFinal party name: {myParty.Name}");
        Console.WriteLine($"Final party members: {myParty.Members.Count}");
        
        foreach (var node in graph.Tasks.OrderBy(n => n.TaskId))
        {
            var outcome = await node.CompletionResult;
            Console.Write($"  - {node.TaskId} -> ");
            
            switch (outcome.Status)
            {
                case TaskResultStatus.Succeeded:
                    Console.WriteLine("Success");
                    break;
                case TaskResultStatus.FailedControlled:
                    Console.WriteLine($"Controlled Failure: {outcome.ControlledError!.Message}");
                    break;
                case TaskResultStatus.FailedUncontrolled:
                    Console.WriteLine($"UNCONTROLLED FAILURE: {outcome.UncontrolledException!.GetType().Name} -> {outcome.UncontrolledException.Message}");
                    break;
            }
        }
    }
}