using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;

namespace NOF;

/// <summary>
/// A decorator for IMigrationsSqlGenerator that filters out migration operations
/// targeting tables belonging to entities marked with [HostOnly].
/// Used in tenant contexts to prevent creating host-only tables in tenant databases.
/// </summary>
internal sealed class NOFTenantMigrationsSqlGenerator : IMigrationsSqlGenerator
{
    private readonly IMigrationsSqlGenerator _inner;

    public NOFTenantMigrationsSqlGenerator(IMigrationsSqlGenerator inner)
    {
        _inner = inner;
    }

    public IReadOnlyList<MigrationCommand> Generate(
        IReadOnlyList<MigrationOperation> operations,
        IModel? model,
        MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default)
    {
        var hostOnlyTableNames = ResolveHostOnlyTableNames(model);

        if (hostOnlyTableNames.Count == 0)
        {
            return _inner.Generate(operations, model, options);
        }

        var filteredOperations = operations
            .Where(op => !IsHostOnlyOperation(op, hostOnlyTableNames))
            .ToList();

        return _inner.Generate(filteredOperations, model, options);
    }

    private static HashSet<string> ResolveHostOnlyTableNames(IModel? model)
    {
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (model == null)
            return tableNames;

        foreach (var entityType in model.GetEntityTypes())
        {
            if (entityType.ClrType.IsDefined(typeof(HostOnlyAttribute)))
            {
                var tableName = entityType.GetTableName();
                if (tableName != null)
                {
                    tableNames.Add(tableName);
                }
            }
        }

        return tableNames;
    }

    private static bool IsHostOnlyOperation(MigrationOperation operation, HashSet<string> hostOnlyTableNames)
    {
        var tableName = GetTableName(operation);
        return tableName != null && hostOnlyTableNames.Contains(tableName);
    }

    private static string? GetTableName(MigrationOperation operation) => operation switch
    {
        CreateTableOperation op => op.Name,
        DropTableOperation op => op.Name,
        AddColumnOperation op => op.Table,
        DropColumnOperation op => op.Table,
        AlterColumnOperation op => op.Table,
        RenameColumnOperation op => op.Table,
        RenameTableOperation op => op.Name,
        AddForeignKeyOperation op => op.Table,
        DropForeignKeyOperation op => op.Table,
        CreateIndexOperation op => op.Table,
        DropIndexOperation op => op.Table,
        AddPrimaryKeyOperation op => op.Table,
        DropPrimaryKeyOperation op => op.Table,
        AddUniqueConstraintOperation op => op.Table,
        DropUniqueConstraintOperation op => op.Table,
        AddCheckConstraintOperation op => op.Table,
        DropCheckConstraintOperation op => op.Table,
        InsertDataOperation op => op.Table,
        UpdateDataOperation op => op.Table,
        DeleteDataOperation op => op.Table,

        _ => null
    };
}
