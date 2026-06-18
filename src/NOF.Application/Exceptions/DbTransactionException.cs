namespace NOF.Application;

/// <summary>
/// Represents a transaction failure in the persistence layer.
/// </summary>
public class DbTransactionException : DbException
{
    public DbTransactionException(string message)
        : base(message)
    {
    }

    public DbTransactionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
