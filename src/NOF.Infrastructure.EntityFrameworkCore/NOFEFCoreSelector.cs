using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NOF.Application.Dependents;

namespace NOF;

public interface INOFEFCoreDbContextSelector<THostApplication>
    where THostApplication : class, IHost
{
    public INOFEFCoreSelector<THostApplication> WithDbContext<TDbContext>()
        where TDbContext : NOFDbContext;
}

internal class NOFEFCoreDbContextSelector<THostApplication> : INOFEFCoreDbContextSelector<THostApplication>
    where THostApplication : class, IHost
{
    private readonly INOFAppBuilder<THostApplication> _builder;
    public NOFEFCoreDbContextSelector(INOFAppBuilder<THostApplication> builder)
    {
        _builder = builder;
    }
    public INOFEFCoreSelector<THostApplication> WithDbContext<TDbContext>() where TDbContext : NOFDbContext
    {
        _builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        _builder.Services.AddScoped<IStateMachineContextRepository, StateMachineContextRepository<TDbContext>>();
        _builder.Services.AddDbContext<TDbContext>(options =>
        {
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new NOFDbContextOptionsExtension(_builder.EventDispatcher));
            _builder.EventDispatcher.Publish(new DbContextConfigurating(options));
        });
        _builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());
        _builder.UseEntityFrameworkCore = true;
        _builder.DbContextType = typeof(TDbContext);
        return new NOFEFCoreSelector<THostApplication>(_builder);
    }
}

public interface INOFEFCoreSelector<THostApplication>
    where THostApplication : class, IHost
{
    public INOFAppBuilder<THostApplication> Builder { get; }
}

internal class NOFEFCoreSelector<THostApplication> : INOFEFCoreSelector<THostApplication>
    where THostApplication : class, IHost
{
    public INOFAppBuilder<THostApplication> Builder { get; }
    public NOFEFCoreSelector(INOFAppBuilder<THostApplication> builder)
    {
        Builder = builder;
    }
}