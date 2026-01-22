namespace NOF;

public interface ICommandRider
{
    public Task SendAsync(ICommand command,
        IDictionary<string, object?>? headers = null,
        string? destinationEndpointName = null,
        CancellationToken cancellationToken = default);
}
