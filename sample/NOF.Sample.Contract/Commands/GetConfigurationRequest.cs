using NOF.Contract;

namespace NOF.Sample;

public record GetConfigurationRequest(string AppName) : IRequest<GetConfigurationResponse>;

public record GetConfigurationResponse(string Content);


