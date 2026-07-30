namespace NOF.Infrastructure;

/// <summary>
/// Records a committed candidate sequence for an ordered outbox key.
/// Concurrent producers may propose the same next sequence, but the composite key allows only one transaction to commit.
/// </summary>
public sealed class NOFOutboxOrderState
{
    public string OrderKey { get; set; } = null!;
    public long Sequence { get; set; }
    public bool CompletesOrderKey { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
