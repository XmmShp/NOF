namespace NOF.Infrastructure;

/// <summary>Configures durable, local file system object storage.</summary>
public sealed class FileSystemObjectStorageOptions
{
    /// <summary>
    /// Gets or sets the storage directory. Relative paths are resolved against the working directory.
    /// Defaults to <c>App_Data/objects</c> under the application base directory.
    /// This directory is owned by the provider and must not be modified externally.
    /// </summary>
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "objects");
}
