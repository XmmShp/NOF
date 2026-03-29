namespace NOF.Sample;

public record GetConfigNodeChildrenRequest(long Id);

public record GetConfigNodeChildrenResponse(long NodeId, List<long> ChildrenIds);

public record GetConfigNodeByIdRequest(long Id);

public record GetConfigNodeByIdResponse(ConfigNodeDto Node);

public record GetRootConfigNodesRequest;

public record GetRootConfigNodesResponse(List<ConfigNodeDto> Nodes);

public record ConfigNodeDto(
    long Id,
    long? ParentId,
    string Name,
    string? ActiveFileName,
    List<ConfigFileDto> ConfigFiles
);

public record ConfigFileDto(
    string Name,
    string Content
);
