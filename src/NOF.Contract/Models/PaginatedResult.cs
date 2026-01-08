namespace NOF;

public interface IPaginatedResult
{
    /// <summary>
    /// 当前页码
    /// </summary>
    int PageNumber { get; init; }

    /// <summary>
    /// 每页大小
    /// </summary>
    int PageSize { get; init; }

    /// <summary>
    /// 总记录数
    /// </summary>
    int TotalCount { get; init; }

    /// <summary>
    /// 总页数
    /// </summary>
    int TotalPages { get; init; }

    /// <summary>
    /// 是否有上一页
    /// </summary>
    bool HasPrevious { get; }

    /// <summary>
    /// 是否有下一页
    /// </summary>
    bool HasNext { get; }
}

/// <summary>
/// 分页结果数据传输对象
/// </summary>
/// <typeparam name="T">分页项的类型</typeparam>
public record PaginatedResult<T> : IPaginatedResult
{
    /// <summary>
    /// 当前页码
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 总记录数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// 是否有上一页
    /// </summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>
    /// 是否有下一页
    /// </summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>
    /// 分页数据项
    /// </summary>
    public required T[] Items { get; init; }
}