using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.ByCode;
using NHibernate.Tool.hbm2ddl;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

namespace NOF.Infrastructure.NHibernate;

internal sealed class NHibernateSessionFactoryRegistry
{
    private static readonly MethodInfo RegisterEntityMethod = typeof(NHibernateSessionFactoryRegistry)
        .GetMethod(nameof(RegisterEntity), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConcurrentDictionary<string, Lazy<ISessionFactory>> SessionFactories = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SchemaLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> InitializedSchemas = new(StringComparer.Ordinal);

    private readonly NHibernateConfigurationOptions _options;
    private readonly IEnumerable<IDbContextModelCreatingContributor> _contributors;
    private readonly ILogger<NHibernateSessionFactoryRegistry> _logger;

    public NHibernateSessionFactoryRegistry(
        IOptions<NHibernateConfigurationOptions> options,
        IEnumerable<IDbContextModelCreatingContributor> contributors,
        ILogger<NHibernateSessionFactoryRegistry> logger)
    {
        _options = options.Value;
        _contributors = contributors;
        _logger = logger;
    }

    public ISession OpenSession(string tenantId)
    {
        tenantId = TenantId.Normalize(tenantId);
        var key = GetFactoryKey(tenantId);
        var sessionFactory = SessionFactories.GetOrAdd(
            key,
            _ => new Lazy<ISessionFactory>(() => BuildSessionFactory(tenantId), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        return sessionFactory.OpenSession();
    }

    private ISessionFactory BuildSessionFactory(string tenantId)
    {
        if (_options.TenantMode != TenantMode.DatabasePerTenant)
        {
            throw new NotSupportedException("NOF.Infrastructure.NHibernate currently supports only TenantMode.DatabasePerTenant.");
        }

        var connectionString = DbConnectionStringTemplateResolver.ResolveTenantId(_options.ConnectionStringTemplate, tenantId);
        var configuration = new Configuration();

        configuration.DataBaseIntegration(db =>
        {
            db.ConnectionString = connectionString;
            db.IsolationLevel = IsolationLevel.ReadCommitted;
        });

        _options.Configure(configuration, connectionString);

        var mapper = new ModelMapper();
        foreach (var definition in BuildModelDefinitions())
        {
            RegisterEntityMethod
                .MakeGenericMethod(definition.EntityType)
                .Invoke(null, [mapper, definition]);
        }

        configuration.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());
        EnsureSchemaInitialized(configuration, connectionString);

        var sessionFactory = configuration.BuildSessionFactory();
        _logger.LogDebug("Created NHibernate session factory for tenant '{TenantId}'.", tenantId);
        return sessionFactory;
    }

    private IReadOnlyCollection<NHibernateEntityDefinition> BuildModelDefinitions()
    {
        var builder = new NHibernateModelDefinitionBuilder();
        foreach (var contributor in _contributors)
        {
            contributor.Configure(builder);
        }

        return builder.Entities;
    }

    private void EnsureSchemaInitialized(Configuration configuration, string connectionString)
    {
        if (!_options.BuildSchemaOnInitialize)
        {
            return;
        }

        var key = connectionString;
        if (InitializedSchemas.ContainsKey(key))
        {
            return;
        }

        var schemaLock = SchemaLocks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        schemaLock.Wait();
        try
        {
            if (InitializedSchemas.ContainsKey(key))
            {
                return;
            }

            new SchemaUpdate(configuration).Execute(false, true);
            InitializedSchemas.TryAdd(key, 0);
        }
        finally
        {
            schemaLock.Release();
        }
    }

    private string GetFactoryKey(string tenantId)
        => $"{_options.TenantMode}|{DbConnectionStringTemplateResolver.ResolveTenantId(_options.ConnectionStringTemplate, tenantId)}";

    private static void RegisterEntity<TEntity>(ModelMapper mapper, NHibernateEntityDefinition definition)
        where TEntity : class
    {
        mapper.Class<TEntity>(map =>
        {
            map.Table(definition.TableName ?? typeof(TEntity).Name);
            map.Lazy(false);

            ConfigureKey(map, definition);

            foreach (var propertyInfo in definition.EntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!propertyInfo.CanRead || !propertyInfo.CanWrite)
                {
                    continue;
                }

                if (definition.KeyPropertyNames.Contains(propertyInfo.Name, StringComparer.Ordinal))
                {
                    continue;
                }

                if (!TryGetPersistentPropertyType(propertyInfo.PropertyType, out _))
                {
                    continue;
                }

                ConfigureProperty(map, definition, propertyInfo);
            }
        });
    }

    private static void ConfigureKey<TEntity>(IClassMapper<TEntity> map, NHibernateEntityDefinition definition)
        where TEntity : class
    {
        if (definition.KeyPropertyNames.Count == 0)
        {
            throw new InvalidOperationException($"Entity '{definition.EntityType.FullName}' does not define a key.");
        }

        if (definition.KeyPropertyNames.Count == 1)
        {
            var propertyInfo = definition.GetProperty(definition.KeyPropertyNames[0]);
            map.Id(propertyInfo.Name, id =>
            {
                id.Column(propertyInfo.Name);
                id.Generator(Generators.Assigned);
                ConfigureType(id, propertyInfo.PropertyType);
            });
            return;
        }

        map.ComposedId(id =>
        {
            foreach (var propertyName in definition.KeyPropertyNames)
            {
                var propertyInfo = definition.GetProperty(propertyName);
                id.Property(propertyInfo.Name, key =>
                {
                    key.Column(propertyInfo.Name);
                    ConfigureType(key, propertyInfo.PropertyType);
                });
            }
        });
    }

    private static void ConfigureProperty<TEntity>(
        IClassMapper<TEntity> map,
        NHibernateEntityDefinition definition,
        PropertyInfo propertyInfo)
        where TEntity : class
    {
        definition.Properties.TryGetValue(propertyInfo.Name, out var propertyDefinition);
        var indexDefinitions = definition.Indexes
            .Where(index => index.PropertyNames.Contains(propertyInfo.Name, StringComparer.Ordinal))
            .ToArray();

        map.Property(propertyInfo.Name, property =>
        {
            property.Column(column =>
            {
                column.Name(propertyInfo.Name);
                if (propertyDefinition?.MaxLength is { } maxLength)
                {
                    column.Length(maxLength);
                }

                if (propertyDefinition?.IsRequired == true || IsNonNullableValueType(propertyInfo.PropertyType))
                {
                    column.NotNullable(true);
                }

                foreach (var index in indexDefinitions)
                {
                    column.Index(BuildIndexName(definition, index));
                    if (index.IsUnique)
                    {
                        column.Unique(true);
                    }
                }
            });

            ConfigureType(property, propertyInfo.PropertyType);
        });
    }

    private static string BuildIndexName(NHibernateEntityDefinition definition, NHibernateIndexDefinition index)
        => $"IX_{definition.TableName ?? definition.EntityType.Name}_{string.Join("_", index.PropertyNames)}";

    private static bool IsNonNullableValueType(Type propertyType)
        => propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null;

    private static bool TryGetPersistentPropertyType(Type propertyType, out Type persistentType)
    {
        persistentType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (persistentType.IsEnum
            || persistentType == typeof(string)
            || persistentType == typeof(byte[])
            || persistentType == typeof(Guid)
            || persistentType == typeof(DateTime)
            || persistentType == typeof(DateTimeOffset)
            || persistentType == typeof(TimeSpan)
            || persistentType == typeof(bool)
            || persistentType == typeof(int)
            || persistentType == typeof(long)
            || persistentType == typeof(short)
            || persistentType == typeof(decimal)
            || persistentType == typeof(double)
            || persistentType == typeof(float))
        {
            return true;
        }

        return TryGetValueObjectType(persistentType, out _);
    }

    private static bool TryGetValueObjectType(Type propertyType, out Type primitiveType)
    {
        var interfaceType = propertyType
            .GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition().FullName == "NOF.Domain.IValueObject`1");

        primitiveType = interfaceType?.GetGenericArguments()[0] ?? typeof(void);
        return interfaceType is not null;
    }

    private static void ConfigureType(dynamic mapper, Type propertyType)
    {
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (!TryGetValueObjectType(underlyingType, out var primitiveType))
        {
            return;
        }

        var userType = typeof(NHibernateValueObjectUserType<,>).MakeGenericType(underlyingType, primitiveType);
        mapper.Type(userType);
    }
}
