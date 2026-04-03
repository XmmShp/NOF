using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NOF.Application;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.Memory;
using NOF.Hosting;
using Xunit;

namespace NOF.Infrastructure.Core.Tests.Steps;

public class InfrastructureDefaultsTests
{
    [Fact]
    public void AddInfrastructureDefaults_ShouldNotRegisterInMemoryPersistenceServicesByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.Services.AddSingleton<IIdGenerator>(new TestIdGenerator());
        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.AddInfrastructureDefaults();

        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetService<MemoryPersistenceStore>().Should().BeNull();
        scope.ServiceProvider.GetService<IUnitOfWork>().Should().BeNull();
        scope.ServiceProvider.GetService<ITransactionManager>().Should().BeNull();
        scope.ServiceProvider.GetService<IInboxMessageRepository>().Should().BeNull();
        scope.ServiceProvider.GetService<IOutboxMessageRepository>().Should().BeNull();
        scope.ServiceProvider.GetService<ITenantRepository>().Should().BeNull();
        scope.ServiceProvider.GetService<IStateMachineContextRepository>().Should().BeNull();
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldNotRegisterMemoryPersistenceWarningHostedServiceByDefault()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();

        var hostedImplementationTypes = builder.Services
            .Where(sd => sd.ServiceType == typeof(IHostedService))
            .Select(sd => sd.ImplementationType)
            .ToList();

        hostedImplementationTypes.Should().NotContain(typeof(MemoryPersistenceWarningHostedService));
    }

    [Fact]
    public void AddInfrastructureDefaults_ShouldRegisterCoreServicesAndOutboxOptions()
    {
        var builder = new TestServiceRegistrationContext();
        builder.AddInfrastructureDefaults();

        using var provider = builder.Services.BuildServiceProvider();

        builder.Services.Should().Contain(service =>
            service.ServiceType == typeof(IHostedService) &&
            service.ImplementationType == typeof(OutboxMessageBackgroundService));
        builder.Services.Should().ContainSingle(service =>
            service.ServiceType == typeof(IHostedService));

        provider.GetRequiredService<IOptions<OutboxOptions>>().Should().NotBeNull();
        provider.GetRequiredService<IEventPublisher>().Should().BeOfType<EventPublisher>();

        builder.Services.Should().NotContain(service => service.ServiceType == typeof(IUnitOfWork));
        builder.Services.Should().NotContain(service => service.ServiceType == typeof(ITransactionManager));
    }

    [Fact]
    public void TryAddCacheService_ShouldNotOverrideExistingNamedRegistration()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<ICacheSerializer, JsonCacheSerializer>();
        services.AddSingleton<ICacheLockRetryStrategy, ExponentialBackoffCacheLockRetryStrategy>();

        services.AddCacheService<TestCacheService>();
        services.TryAddCacheService<MemoryCacheService>();

        var descriptors = services
            .Where(service => service.ServiceType == typeof(ICacheService) && Equals(service.ServiceKey, ICacheServiceFactory.DefaultName))
            .ToList();

        descriptors.Should().HaveCount(1);
    }

    private sealed class TestServiceRegistrationContext : INOFAppBuilder
    {
        private readonly IServiceCollection _services;
        private readonly ConfigurationManager _configuration;
        private readonly IHostEnvironment _environment;
        private readonly ILoggingBuilder _logging;
        private readonly IMetricsBuilder _metrics;
        private readonly Dictionary<object, object> _properties;
        private readonly List<IServiceRegistrationStep> _registrationSteps;
        private readonly List<IApplicationInitializationStep> _initializationSteps;

        public TestServiceRegistrationContext()
        {
            _services = new ServiceCollection();
            _services.AddLogging();
            _services.AddMetrics();
            _configuration = new ConfigurationManager();
            _environment = new TestHostEnvironment();
            _logging = new TestLoggingBuilder(_services);
            _metrics = new TestMetricsBuilder(_services);
            _properties = [];
            _registrationSteps = [];
            _initializationSteps = [];
        }

        public TestServiceRegistrationContext(TestServiceRegistrationContext other)
        {
            _services = other._services;
            _configuration = other._configuration;
            _environment = other._environment;
            _logging = other._logging;
            _metrics = other._metrics;
            _properties = other._properties;
            _registrationSteps = other._registrationSteps;
            _initializationSteps = other._initializationSteps;
        }

        public INOFAppBuilder AddRegistrationStep(IServiceRegistrationStep registrationStep)
        {
            _registrationSteps.Add(registrationStep);
            return this;
        }

        public INOFAppBuilder RemoveRegistrationStep(Predicate<IServiceRegistrationStep> predicate)
        {
            _registrationSteps.RemoveAll(predicate);
            return this;
        }

        public INOFAppBuilder AddInitializationStep(IApplicationInitializationStep initializationStep)
        {
            _initializationSteps.Add(initializationStep);
            return this;
        }

        public INOFAppBuilder RemoveInitializationStep(Predicate<IApplicationInitializationStep> predicate)
        {
            _initializationSteps.RemoveAll(predicate);
            return this;
        }

        public IDictionary<object, object> Properties => _properties;

        public IConfigurationManager Configuration => _configuration;

        public IHostEnvironment Environment => _environment;

        public ILoggingBuilder Logging => _logging;

        public IMetricsBuilder Metrics => _metrics;

        public IServiceCollection Services => _services;

        public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
            where TContainerBuilder : notnull
        {
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "NOF.Infrastructure.Core.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLoggingBuilder(IServiceCollection services) : ILoggingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class TestMetricsBuilder(IServiceCollection services) : IMetricsBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class NullFileProvider : IFileProvider
    {
        public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

        public IFileInfo GetFileInfo(string subpath) => new NotFoundFileInfo(subpath);

        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        private long _current = 1000;

        public long NextId() => Interlocked.Increment(ref _current);
    }

    private sealed class TestCacheService : ICacheService
    {
        public byte[]? Get(string key) => null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => Task.CompletedTask;

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public void Remove(string key)
        {
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
            => Task.CompletedTask;

        public ValueTask<Optional<T>> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Optional<T>>(Optional.None);

        public ValueTask<T> GetOrSetAsync<T>(string key, Func<CancellationToken, ValueTask<T>> factory, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            => factory(cancellationToken);

        public ValueTask SetAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(false);

        public ValueTask<IReadOnlyDictionary<string, Optional<T>>> GetManyAsync<T>(IEnumerable<string> keys, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyDictionary<string, Optional<T>>>(new Dictionary<string, Optional<T>>());

        public ValueTask SetManyAsync<T>(IDictionary<string, T> items, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<long> RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(0L);

        public ValueTask<long> IncrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(value);

        public ValueTask<long> DecrementAsync(string key, long value = 1, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(-value);

        public ValueTask<bool> SetIfNotExistsAsync<T>(string key, T value, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);

        public ValueTask<Optional<T>> GetAndSetAsync<T>(string key, T newValue, DistributedCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Optional<T>>(Optional.None);

        public ValueTask<Optional<T>> GetAndRemoveAsync<T>(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Optional<T>>(Optional.None);

        public ValueTask<Optional<TimeSpan>> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Optional<TimeSpan>>(Optional.None);

        public ValueTask<bool> SetTimeToLiveAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);

        public ValueTask<IDistributedLock> AcquireLockAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IDistributedLock>(new TestDistributedLock(key));

        public ValueTask<Optional<IDistributedLock>> TryAcquireLockAsync(string key, TimeSpan expiration, TimeSpan timeout, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Optional.Of<IDistributedLock>(new TestDistributedLock(key)));
    }

    private sealed class TestDistributedLock(string key) : IDistributedLock
    {
        public string Key { get; } = key;

        public bool IsAcquired => true;

        public ValueTask<bool> ReleaseAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }
}
