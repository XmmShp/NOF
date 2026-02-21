using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NOF.Infrastructure.EntityFrameworkCore;

[Table(nameof(EFCoreStateMachineContext))]
[PrimaryKey(nameof(CorrelationId), nameof(DefinitionType))]
internal sealed class EFCoreStateMachineContext
{
    [Required]
    public required string CorrelationId { get; set; }

    [Required]
    public required string DefinitionType { get; set; }

    public required int State { get; set; }
}
