namespace NOF.Sample;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record CreateConfigNodeRequest(string Name, long? ParentId) : IRequest;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record DeleteConfigNodeRequest(long Id) : IRequest;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record AddOrUpdateConfigFileRequest(long NodeId, string FileName, string Content) : IRequest;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record RemoveConfigFileRequest(long NodeId, string FileName) : IRequest;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record SetActiveFileRequest(long NodeId, string? FileName) : IRequest;
