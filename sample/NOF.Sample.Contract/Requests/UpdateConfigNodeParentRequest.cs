namespace NOF.Sample;

public record UpdateConfigNodeParentRequest
{
    public long NodeId { get; set; }
    public long? NewParentId { get; set; }
}
