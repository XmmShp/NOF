namespace NOF.Hosting;

/// <summary>
/// Represents the partial-order relationship between two topologizable instances.
/// </summary>
public enum TopologyComparison
{
    DoesNotMatter = 0,
    Before = 1,
    After = 2
}

/// <summary>
/// Describes runtime topology metadata for a contract that participates in dependency-aware ordering.
/// </summary>
public interface ITopologizable<TContract>
{
    /// <summary>
    /// Compares the current instance with another contract instance and returns their partial-order relationship.
    /// </summary>
    TopologyComparison Compare(TContract other);
}
