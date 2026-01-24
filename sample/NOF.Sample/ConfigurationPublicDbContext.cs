using Microsoft.EntityFrameworkCore;

namespace NOF.Sample;

public class ConfigurationPublicDbContext : NOFPublicDbContext
{
    public ConfigurationPublicDbContext(DbContextOptions<ConfigurationPublicDbContext> options) : base(options)
    {
    }
}