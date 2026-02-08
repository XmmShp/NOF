namespace NOF;

/// <summary>
/// NOF DbContext factory options.
/// </summary>
public class DbContextFactoryOptions
{
    /// <summary>
    /// Whether to automatically migrate the database.
    /// </summary>
    public bool AutoMigrate { get; set; } = false;
}
