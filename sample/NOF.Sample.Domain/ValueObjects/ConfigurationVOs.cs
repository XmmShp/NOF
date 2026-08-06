using NOF.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NOF.Sample;

[ValueObjectLength(100, MinimumLength = 1)]
public readonly partial struct ConfigNodeName : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainValidationException("Node name cannot be empty.");
        }
    }
}

[ValueObjectLength(100, MinimumLength = 1)]
public readonly partial struct ConfigFileName : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainValidationException("File name cannot be empty.");
        }
    }
}

public readonly partial struct ConfigContent : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainValidationException("Config content cannot be empty.");
        }

        try
        {
            var node = JsonNode.Parse(input);
            if (node is not JsonObject)
            {
                throw new DomainValidationException("Config content must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            throw new DomainValidationException("Invalid JSON format.");
        }
    }
}
