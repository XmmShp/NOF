namespace NOF.Sample;

[AllowAnonymous]
[ExposeToHttpEndpoint(HttpVerb.Post)]
public record UpdateConfigNodeParentRequest(long NodeId, long? NewParentId) : IRequest;
