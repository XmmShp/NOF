using Microsoft.Extensions.Configuration;
using System.Text;

namespace NOF.Sample;

internal class ConfigurationCenterRegistrationStep : IBaseSettingsServiceRegistrationStep
{
    private readonly string _systemName;

    public ConfigurationCenterRegistrationStep(string systemName)
    {
        _systemName = systemName;
    }

    public async ValueTask ExecuteAsync(INOFAppBuilder buider)
    {
        ArgumentNullException.ThrowIfNull(buider.RequestSender);
        var response = await buider.RequestSender.SendAsync(new GetConfigurationRequest(_systemName));
        if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Value.Content))
        {
            return;
        }

        var buffer = Encoding.UTF8.GetBytes(response.Value.Content);
        var ms = new MemoryStream(buffer);

        buider.Configuration.AddJsonStream(ms);
    }
}