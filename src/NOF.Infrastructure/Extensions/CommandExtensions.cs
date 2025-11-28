namespace NOF;

public static class CommandExtensions
{
    private static readonly EndpointNameFormatter Formatter = EndpointNameFormatter.Instance;

    extension(ICommandBase command)
    {
        public Uri GetQueueUri()
        {
            var queueName = Formatter.GetMessageName(command);
            var uriString = $"queue:{queueName}";
            return new Uri(uriString);
        }
    }
}
