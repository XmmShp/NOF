namespace NOF.Sample;

public record GetConfigurationCommand(string AppName) : ICommand<GetConfigurationResponse>;

public record GetConfigurationResponse(string Content);


