using Microsoft.Extensions.Configuration;
using System.Text;

namespace NOF.Sample;

internal class ConfigurationCenterConfigurator : IConfiguringServicesConfigurator
{
    private readonly string _systemName;

    public ConfigurationCenterConfigurator(string systemName)
    {
        _systemName = systemName;
    }

    public async ValueTask ExecuteAsync(INOFApp app)
    {
        ArgumentNullException.ThrowIfNull(app.CommandSender);
        var response = await app.CommandSender.SendAsync(new GetConfigurationCommand(_systemName));
        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Value.Content))
        {
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(response.Value.Content);
        var ms = new MemoryStream(buffer);

        app.Unwrap().Configuration.AddJsonStream(ms);
    }
}