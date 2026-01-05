using System.ComponentModel.DataAnnotations;

namespace NOF;

/// <summary>
/// 分页结果查询请求基类
/// </summary>
public record PaginatedRequest<T> : IRequest<T>
    where T : class, IPaginatedResult
{
    /// <summary>
    /// 页码，从1开始
    /// </summary>
    [Required]
    public required int PageNumber { get; init; }

    /// <summary>
    /// 每页大小
    /// </summary>
    [Required]
    public required int PageSize { get; init; }
}