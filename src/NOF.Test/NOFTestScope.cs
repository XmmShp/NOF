using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Security.Claims;

namespace NOF.Test;

public sealed class NOFTestScope : IAsyncDisposable, IDisposable
{
    private readonly AsyncServiceScope _scope;

    public NOFTestScope(AsyncServiceScope scope)
    {
        _scope = scope;
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public T GetRequiredService<T>() where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    public Context Context => GetRequiredService<IContextAccessor>().Context;

    public IUserContext UserContext => GetRequiredService<IUserContext>();

    public NOFTestScope SetTenant(string? tenantId)
    {
        var accessor = GetRequiredService<IContextAccessor>();
        accessor.Context = accessor.Context.WithTenantId(TenantId.Normalize(tenantId));
        return this;
    }

    public NOFTestScope SetTracing(string? traceId, string? spanId)
    {
        if (traceId is not null && spanId is not null)
        {
            var accessor = GetRequiredService<IContextAccessor>();
            accessor.Context = accessor.Context.WithTracingInfo(new TracingInfo(traceId, spanId));
        }
        return this;
    }

    public NOFTestScope SetUser(ClaimsPrincipal user)
    {
        UserContext.Logout();
        UserContext.User.AddIdentities(user.Identities.OfType<ClaimsIdentity>());
        return this;
    }

    public NOFTestScope SetAnonymousUser()
    {
        UserContext.Logout();
        return this;
    }

    public NOFTestScope SetUser(string userId, string? name = null, IEnumerable<string>? permissions = null, string authenticationType = "Test")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId)
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        if (permissions is not null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim(ClaimTypes.Permission, permission));
            }
        }

        return SetUser(new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType)));
    }
    public Task SendAsync<TCommand>(TCommand command, Context context, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<ICommandSender>().SendAsync(command, context, cancellationToken);
    }

    public Task PublishAsync<TNotification>(TNotification notification, Context context, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<INotificationPublisher>().PublishAsync(notification, context, cancellationToken);
    }

    public void Dispose()
    {
        _scope.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _scope.DisposeAsync();
    }
}
