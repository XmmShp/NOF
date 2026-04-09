namespace NOF.Sample;

public record CreateConfigNodeRequest
{
    public required string Name { get; set; }
    public long? ParentId { get; set; }
}

public record DeleteConfigNodeRequest
{
    public long Id { get; set; }
}

public record AddOrUpdateConfigFileRequest
{
    public long NodeId { get; set; }
    public required string FileName { get; set; }
    public required string Content { get; set; }
}

public record RemoveConfigFileRequest
{
    public long NodeId { get; set; }
    public required string FileName { get; set; }
}

public record SetActiveFileRequest
{
    public long NodeId { get; set; }
    public string? FileName { get; set; }
}
