namespace NOF.Infrastructure;

public sealed class NOFInboxOrderState
{
    public string Route { get; set; } = null!;
    public string OrderKey { get; set; } = null!;
    public long NextSequence { get; set; } = 1;
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimExpiresAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public long? BlockedSequence { get; set; }
    public string? ErrorMessage { get; set; }
}
