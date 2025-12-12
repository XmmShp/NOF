namespace NOF;

[AttributeUsage(AttributeTargets.Class)]
public class EndpointNameAttribute : Attribute
{
    public EndpointNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
