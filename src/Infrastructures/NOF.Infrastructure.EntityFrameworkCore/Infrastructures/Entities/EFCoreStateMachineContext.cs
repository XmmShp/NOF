using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF;

[Table(nameof(EFCoreStateMachineContext))]
[PrimaryKey(nameof(CorrelationId), nameof(DefinitionType))]
internal sealed class EFCoreStateMachineContext
{
    [Required]
    public required string CorrelationId { get; set; }

    [Required]
    public required string DefinitionType { get; set; }

    [Required]
    [MaxLength(1024)]
    public required string ContextType { get; set; }

    [Required]
    public required string ContextData { get; set; }

    public required int State { get; set; }
}
