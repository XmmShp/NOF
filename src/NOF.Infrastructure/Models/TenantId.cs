using NOF.Abstraction;
using NOF.Domain;

namespace NOF.Infrastructure;

public readonly partial struct TenantId : IValueObject<string>
{
    public static TenantId Host { get; } = Of(NOFAbstractionConstants.Tenant.HostId);

    public static string Normalize(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId)
            ? (string)Host
            : (string)Of(tenantId);
    }

    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("TenantId cannot be null or whitespace.", nameof(value));
        }

        if (value[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("TenantId must start with a lowercase letter.", nameof(value));
        }

        foreach (var character in value)
        {
            if ((character < 'a' || character > 'z') && (character < '0' || character > '9'))
            {
                throw new ArgumentException("TenantId must contain only lowercase letters and digits.", nameof(value));
            }
        }
    }
}
