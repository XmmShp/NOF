using System.Diagnostics.CodeAnalysis;
namespace NOF.Sample;

public record GetConfigNodeChildrenRequest
{
    public long Id { get; set; }
}

public record GetConfigNodeChildrenResponse
{
    public long NodeId { get; set; }
    public required List<long> ChildrenIds { get; set; }
}

public record GetConfigNodeByIdRequest
{
    public long Id { get; set; }
}

public record GetConfigNodeByIdResponse
{
    public required ConfigNodeDto Node { get; set; }
}

public record GetRootConfigNodesRequest { }

public record GetRootConfigNodesResponse
{
    public required List<ConfigNodeDto> Nodes { get; set; }
}

public record ConfigNodeDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public required string Name { get; set; }
    public string? ActiveFileName { get; set; }
    public required List<ConfigFileDto> ConfigFiles { get; set; }
}

public record ConfigFileDto
{
    public required string Name { get; set; }
    public required string Content { get; set; }
}
