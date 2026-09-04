namespace NOF.Contract;

public static class NOFSampleResultExtensions
{
    extension(IResult result)
    {
        public string GetRpcFailureMessage()
        {
            ArgumentNullException.ThrowIfNull(result);

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                return result.Message;
            }

            return int.TryParse(result.ErrorCode, out var statusCode)
                ? $"请求失败，状态码: {statusCode}"
                : "请求失败";
        }
    }
}
