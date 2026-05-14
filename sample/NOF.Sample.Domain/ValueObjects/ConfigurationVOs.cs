using NOF.Domain;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NOF.Sample;

public readonly partial struct ConfigNodeName : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainValidationException("Node name cannot be empty.");
        }

        if (input.Length > 100)
        {
            throw new DomainValidationException("Node name cannot exceed 100 characters.");
        }
    }
}

public readonly partial struct ConfigFileName : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainValidationException("File name cannot be empty.");
        }

        if (input.Length > 100)
        {
            throw new DomainValidationException("File name cannot exceed 100 characters.");
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
