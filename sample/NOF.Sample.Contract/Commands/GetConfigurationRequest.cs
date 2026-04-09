namespace NOF.Sample;

public record GetConfigurationRequest
{
    public required string AppName { get; set; }
}

public record GetConfigurationResponse
{
    public required string Content { get; set; }
}
