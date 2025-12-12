namespace NOF;

public class DomainException : Exception
{
    public int ErrorCode { get; }

    public DomainException(Failure failure) : this(failure.ErrorCode, failure.Message)
    {
    }

    public DomainException(Failure failure, Exception innerException) : this(failure.ErrorCode, failure.Message, innerException)
    {
    }

    public DomainException(int errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(int errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}