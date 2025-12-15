using Microsoft.EntityFrameworkCore;

namespace NOF;

public record DbContextModelCreating(ModelBuilder Builder);

public abstract class NOFDbContext : DbContext
{
    protected readonly IEventDispatcher Dispatcher;
    protected NOFDbContext(IEventDispatcher dispatcher, DbContextOptions options) : base(options)
    {
        Dispatcher = dispatcher;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        Dispatcher.Publish(new DbContextModelCreating(modelBuilder));
    }
}
