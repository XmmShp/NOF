namespace NOF.Application;

/// <summary>
/// Represents an optimistic concurrency failure while saving updates.
/// </summary>
public sealed class DbUpdateConcurrencyException : DbUpdateException
{
    public DbUpdateConcurrencyException(string message)
        : base(message)
    {
    }

    public DbUpdateConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
