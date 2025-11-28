using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace NOF;

public class SaveChangesFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly DbContext _dbContext;

    public SaveChangesFilter(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        await next.Send(context);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("save-changes");
    }
}
