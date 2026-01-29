namespace NOF.Sample;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record CreateConfigNodeRequest(string Name, long? ParentId) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record DeleteConfigNodeRequest(long Id) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record AddOrUpdateConfigFileRequest(long NodeId, string FileName, string Content) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record RemoveConfigFileRequest(long NodeId, string FileName) : IRequest;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record SetActiveFileRequest(long NodeId, string? FileName) : IRequest;
