using Microsoft.EntityFrameworkCore;

namespace NOF.Infrastructure;

public sealed class DbContextOptionsConfiguration
{
    public required Action<IServiceProvider, DbContextOptionsBuilder, string, TenantMode> Configure { get; init; }
}
