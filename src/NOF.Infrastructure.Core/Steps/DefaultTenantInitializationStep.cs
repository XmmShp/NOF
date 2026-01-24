using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NOF;

/// <summary>
/// Default tenant initialization implementation
/// </summary>
public class DefaultTenantInitializationStep : IPublicDataSeedInitializationStep
{
    public async Task ExecuteAsync(INOFAppBuilder builder, IHost app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DefaultTenantInitializationStep>>();
        try
        {
            var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();

            // 检查是否已存在默认租户
            var existingTenant = await tenantRepository.FindAsync("default");

            if (existingTenant is not null)
            {
                logger.LogDebug("Default tenant already exists");
                return;
            }

            // 创建默认租户
            var defaultTenant = new Tenant
            {
                Id = "default",
                Name = "Default Tenant",
                Description = "Default system tenant",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            tenantRepository.Add(defaultTenant);

            logger.LogInformation("Created default tenant with ID: {TenantId}", defaultTenant.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create default tenant");
            throw;
        }
    }
}
