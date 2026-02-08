using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

/// <summary>
/// 收件箱消息实体
/// 用于记录需要可靠处理的消息
/// </summary>
[Table(nameof(EFCoreInboxMessage))]
[Index(nameof(CreatedAt))]
internal sealed class EFCoreInboxMessage
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 消息创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
