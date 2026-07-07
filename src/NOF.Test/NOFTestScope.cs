using Microsoft.Extensions.DependencyInjection;
using NOF.Abstraction;
using NOF.Application;
using NOF.Contract;
using NOF.Infrastructure;
using System.Diagnostics;
using System.Security.Claims;

namespace NOF.Test;

public sealed class NOFTestScope : IAsyncDisposable, IDisposable
{
    private readonly AsyncServiceScope _scope;
    private Context _context = Context.Empty;
    private Activity? _tracingActivity;
    private IDisposable? _tenantScope;

    public NOFTestScope(AsyncServiceScope scope)
    {
        _scope = scope;
    }

    public IServiceProvider Services => _scope.ServiceProvider;

    public T GetRequiredService<T>() where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    public Context Context => _context;

    public IUserContext UserContext => GetRequiredService<IUserContext>();

    public NOFTestScope SetTenant(string? tenantId)
    {
        _tenantScope?.Dispose();
        _tenantScope = GetRequiredService<IMutableCurrentTenant>().PushTenant(TenantId.Normalize(tenantId));
        return this;
    }

    public NOFTestScope SetTracing(string? traceId, string? spanId)
    {
        if (traceId is not null && spanId is not null)
        {
            _tracingActivity?.Dispose();
            _tracingActivity = CreateTracingActivity(traceId, spanId);
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

    public NOFTestScope SetContext(Context context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        return this;
    }

    public NOFTestScope SetContextItem(object key, object? value)
    {
        _context = Context.WithItem(key, value);
        return this;
    }

    public NOFTestScope SetContextItems(IReadOnlyDictionary<object, object?> items)
    {
        _context = Context.WithItems(items);
        return this;
    }

    public NOFTestScope RemoveContextItem(object key)
    {
        _context = Context.WithoutItem(key);
        return this;
    }

    public NOFTestScope SetUser(string userId, string? name = null, IEnumerable<string>? permissions = null, string authenticationType = "Test")
    {
        var claims = new List<Claim>
        {
            new("sub", userId)
        };

        if (!string.IsNullOrWhiteSpace(name))
        {
            claims.Add(new Claim("name", name));
        }

        if (permissions is not null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim(ClaimTypes.Permission, permission));
            }
        }

        return SetUser(new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType,
            nameType: "name",
            roleType: ClaimTypes.Role)));
    }

    public TClient GetRpcClient<TClient>() where TClient : notnull
    {
        return GetRequiredService<TClient>();
    }

    public Task<TResult> CallAsync<TClient, TResult>(
        Func<TClient, Context, CancellationToken, Task<TResult>> invocation,
        Context? context = null,
        CancellationToken cancellationToken = default)
        where TClient : notnull
    {
        ArgumentNullException.ThrowIfNull(invocation);

        return invocation(GetRpcClient<TClient>(), context ?? Context, cancellationToken);
    }

    public Task SendAsync<TCommand>(TCommand command, Context? context = null, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<ICommandSender>().SendAsync(command, context ?? Context, cancellationToken);
    }

    public Task PublishAsync<TNotification>(TNotification notification, Context? context = null, CancellationToken cancellationToken = default)
    {
        return GetRequiredService<INotificationPublisher>().PublishAsync(notification, context ?? Context, cancellationToken);
    }

    public void Dispose()
    {
        _tenantScope?.Dispose();
        _tracingActivity?.Dispose();
        _scope.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        _tenantScope?.Dispose();
        _tracingActivity?.Dispose();
        return _scope.DisposeAsync();
    }

    private static Activity CreateTracingActivity(string traceId, string spanId)
    {
        var activity = new Activity("NOF.Test.Scope");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.SetParentId($"00-{traceId}-{spanId}-01");
        activity.Start();
        return activity;
    }
}
