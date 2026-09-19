namespace NOF.Hosting.AspNetCore;
/// <summary>Describes the RPC operation associated with an HTTP endpoint.</summary>

public sealed class NofRpcHttpEndpointMetadata
{
    public static NofRpcHttpEndpointMetadata Instance { get; } = new();

    private NofRpcHttpEndpointMetadata()
    {
    }
}
