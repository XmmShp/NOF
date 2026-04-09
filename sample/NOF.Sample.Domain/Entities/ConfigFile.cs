namespace NOF.Sample;

public class ConfigFile
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
