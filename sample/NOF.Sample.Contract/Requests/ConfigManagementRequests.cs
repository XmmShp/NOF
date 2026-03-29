namespace NOF.Sample;

public record CreateConfigNodeRequest(string Name, long? ParentId);

public record DeleteConfigNodeRequest(long Id);

public record AddOrUpdateConfigFileRequest(long NodeId, string FileName, string Content);

public record RemoveConfigFileRequest(long NodeId, string FileName);

public record SetActiveFileRequest(long NodeId, string? FileName);
