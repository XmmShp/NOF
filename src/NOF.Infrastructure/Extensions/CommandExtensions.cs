namespace NOF;

public static partial class __NOF_Infrastructure_Extensions__
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
