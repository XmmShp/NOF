namespace NOF;

public class DomainException : Exception
{
    public int ErrorCode { get; }
    public DomainException(Failure failure) : this(failure.ErrorCode, failure.Message)
    {
    }
    public DomainException(int errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}