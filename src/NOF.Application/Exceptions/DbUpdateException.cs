namespace NOF.Application;

/// <summary>
/// Represents a persistence failure while saving updates.
/// </summary>
public class DbUpdateException : DbException
{
    public DbUpdateException(string message)
        : base(message)
    {
    }

    public DbUpdateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
