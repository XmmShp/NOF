using Microsoft.AspNetCore.Builder;

namespace NOF;

public class JwtAuthenticationConfigurator : IAuthenticationConfigurator
{
    public Task ExecuteAsync(INOFApp app, WebApplication webApp)
    {
        webApp.UseAuthentication();
        webApp.UseMiddleware<JwtUserInfoMiddleware>();
        webApp.UseMiddleware<PermissionAuthorizationMiddleware>();
        webApp.UseAuthorization();
        return Task.CompletedTask;
    }
}