using NOF.Domain;
using System.ComponentModel;

namespace NOF.Application;

/// <summary>
/// Repository for persisting state machine contexts. Not intended for direct use.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IStateMachineContextRepository : IRepository<NOFStateMachineContext, string, string>;
