namespace NOF.Hosting.AspNetCore;

internal sealed class NofRpcHttpEndpointMetadata
{
    public static NofRpcHttpEndpointMetadata Instance { get; } = new();

    private NofRpcHttpEndpointMetadata()
    {
    }
}
