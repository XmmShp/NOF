namespace NOF.Application;

/// <summary>
/// Represents a failure while committing a transaction.
/// </summary>
public sealed class DbTransactionCommitException : DbTransactionException
{
    public DbTransactionCommitException(string message)
        : base(message)
    {
    }

    public DbTransactionCommitException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
