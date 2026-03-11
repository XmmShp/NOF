using Microsoft.Extensions.DependencyInjection;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure.Core;
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

    public IMutableInvocationContext InvocationContext => GetRequiredService<IMutableInvocationContext>();

    public IMutableUserContext UserContext => GetRequiredService<IMutableUserContext>();

    public NOFTestScope SetTenant(string? tenantId)
    {
        InvocationContext.SetTenantId(tenantId);
        return this;
    }

    public NOFTestScope SetTracing(string? traceId, string? spanId)
    {
        InvocationContext.SetTracingInfo(traceId, spanId);
        return this;
    }

    public NOFTestScope SetUser(ClaimsPrincipal user)
    {
        UserContext.SetUser(user);
        return this;
    }

    public NOFTestScope SetAnonymousUser()
    {
        UserContext.UnsetUser();
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

    public Task<Result> SendAsync(IRequest request, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<IRequestSender>().SendAsync(request, cancellationToken);
    }

    public Task<Result<TResponse>> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<IRequestSender>().SendAsync(request, cancellationToken);
    }

    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<ICommandSender>().SendAsync(command, cancellationToken);
    }

    public Task PublishAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<INotificationPublisher>().PublishAsync(notification, cancellationToken);
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
