using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

/// <summary>
/// Inbox message entity used for tracking reliably processed messages.
/// </summary>
[Table(nameof(EFCoreInboxMessage))]
[Index(nameof(CreatedAt))]
internal sealed class EFCoreInboxMessage
{
    /// <summary>
    /// The unique message identifier.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The message creation time.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
