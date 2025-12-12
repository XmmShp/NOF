namespace NOF.Sample;

[ExposeToHttpEndpoint(HttpVerb.Post)]
public record UpdateConfigNodeParentRequest(long NodeId, long? NewParentId) : IRequest;
