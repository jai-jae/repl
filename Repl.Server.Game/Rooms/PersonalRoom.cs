namespace Repl.Server.Game.Managers.Rooms;

public class PersonalRoom : ITickable, IDisposable
{
    private bool disposed;
    
    public long Id { get; }
    public TickRate RequiredTickRate { get; } = TickRate.Low;

    public PersonalRoom(long id)
    {
        this.Id = id;
    }
    
    public bool ShouldTick()
    {
        return true;
    }

    public void Tick(TickContext context)
    {
        
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
}