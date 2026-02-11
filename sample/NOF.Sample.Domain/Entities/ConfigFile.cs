using NOF.Domain;

namespace NOF.Sample;

[Snapshotable]
public class ConfigFile : Entity
{
    public ConfigFileName Name { get; init; }
    public ConfigContent Content { get; private set; }

    internal ConfigFile() { }

    public ConfigFile(ConfigFileName name, ConfigContent content)
    {
        Name = name;
        Content = content;
    }

    public void UpdateContent(ConfigContent content)
    {
        Content = content;
    }
}
