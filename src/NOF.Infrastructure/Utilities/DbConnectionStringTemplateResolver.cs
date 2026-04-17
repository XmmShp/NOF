namespace NOF.Infrastructure;

public static class DbConnectionStringTemplateResolver
{
    public static string ResolveTenantId(string value, string tenantId)
        => value.Replace("{tenantId}", TenantId.Normalize(tenantId), StringComparison.OrdinalIgnoreCase);
}
