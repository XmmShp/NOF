using System.Diagnostics.CodeAnalysis;

namespace NOF.Application;

/// <summary>
/// Describes a source-generated state machine notification handler.
/// Used by <c>HandlerSelector.AddStateMachineHandlers</c> to register these handlers
/// without the Application layer needing to reference <c>HandlerInfos</c>.
/// </summary>
public record StateMachineHandlerEntry(
    [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerType,
    Type NotificationType);
