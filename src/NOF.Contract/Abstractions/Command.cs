using NOF.Contract.Annotations;

namespace NOF;

/// <summary>
/// Marker interface for command messages (fire-and-forget).
/// </summary>
public interface ICommand : IMessage;