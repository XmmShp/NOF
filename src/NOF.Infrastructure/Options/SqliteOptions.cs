namespace NOF.Infrastructure;

public class SqliteOptions
{
    public string ConnectionStringName { get; set; } = "sqlite";

    public bool UseInMemory { get; set; }

    public string InMemoryDatabaseName { get; set; } = "nof-sqlite-memory";
}
