using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace NOF.Infrastructure.EntityFrameworkCore;

internal sealed class NOFTenantMigrationsModelDiffer : IMigrationsModelDiffer
{
    private readonly IMigrationsModelDiffer _inner;

    public NOFTenantMigrationsModelDiffer(IMigrationsModelDiffer inner)
    {
        _inner = inner;
    }

    public bool HasDifferences(IRelationalModel? source, IRelationalModel? target)
        => _inner.HasDifferences(source, target);

    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        var operations = _inner.GetDifferences(source, target).ToList();
        if (target is null)
        {
            return operations;
        }

        var tenantScopedTables = target.Tables
            .Where(table => table.EntityTypeMappings.Any(mapping =>
                mapping.TypeBase is IReadOnlyEntityType entityType
                && TenantModelHelper.IsTenantScopedEntity(entityType)))
            .ToDictionary(
                table => GetTableKey(table.Schema, table.Name),
                table => table,
                StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations.OfType<CreateTableOperation>())
        {
            if (!tenantScopedTables.TryGetValue(GetTableKey(operation.Schema, operation.Name), out var table))
            {
                continue;
            }

            if (operation.Columns.Any(column => string.Equals(column.Name, TenantModelHelper.TenantIdPropertyName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var tenantColumn = table.Columns.FirstOrDefault(column => string.Equals(column.Name, TenantModelHelper.TenantIdPropertyName, StringComparison.OrdinalIgnoreCase));
            if (tenantColumn is null)
            {
                continue;
            }

            operation.Columns.Add(new AddColumnOperation
            {
                Name = tenantColumn.Name,
                ClrType = tenantColumn.StoreTypeMapping.ClrType,
                ColumnType = tenantColumn.StoreType,
                IsNullable = tenantColumn.IsNullable,
                MaxLength = tenantColumn.PropertyMappings.Select(mapping => mapping.Property.GetMaxLength()).FirstOrDefault()
            });
        }

        return operations;
    }

    private static string GetTableKey(string? schema, string name)
        => $"{schema ?? string.Empty}.{name}";
}
