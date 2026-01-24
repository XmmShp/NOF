namespace NOF;

/// <summary>
/// Repository factory interface for creating repository bundles
/// </summary>
public interface IRepositoryFactory
{
    /// <summary>
    /// Gets a repository bundle for the specified tenant
    /// </summary>
    RepositoryBundle<TRepository> GetRepositoryBundle<TRepository>(string tenantId)
        where TRepository : IRepository;
}
