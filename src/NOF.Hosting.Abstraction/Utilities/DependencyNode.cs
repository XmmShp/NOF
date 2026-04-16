namespace NOF.Hosting;

public sealed record DependencyNode(object ExtraInfo, IReadOnlyCollection<Type> AllAssignableTypes);
