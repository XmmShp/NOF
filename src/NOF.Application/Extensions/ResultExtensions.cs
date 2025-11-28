namespace NOF;

public static class ResultExtensions
{
    extension(Result)
    {
        public static FailResult Fail(Failure failure)
        {
            return Result.Fail(failure.ErrorCode, failure.Message);
        }
    }
}
