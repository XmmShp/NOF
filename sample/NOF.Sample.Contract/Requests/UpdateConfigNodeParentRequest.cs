namespace NOF.Sample;

[ExposeToHttpEndpoint(HttpVerb.Post, AllowAnonymous = true)]
public record UpdateConfigNodeParentRequest(long NodeId, long? NewParentId) : IRequest;
