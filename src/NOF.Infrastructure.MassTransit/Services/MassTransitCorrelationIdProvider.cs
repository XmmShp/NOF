using MassTransit;

namespace NOF;

internal class MassTransitCorrelationIdProvider : ICorrelationIdProvider
{
    private readonly ConsumeContext _context;
    public MassTransitCorrelationIdProvider(ConsumeContext context)
    {
        _context = context;
    }
    public string CorrelationId => _context.ConversationId?.ToString() ?? string.Empty;
}
