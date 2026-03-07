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
            throw new ValidationException("Node name cannot be empty.");
        }

        if (input.Length > 100)
        {
            throw new ValidationException("Node name cannot exceed 100 characters.");
        }
    }
}

public readonly partial struct ConfigFileName : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ValidationException("File name cannot be empty.");
        }

        if (input.Length > 100)
        {
            throw new ValidationException("File name cannot exceed 100 characters.");
        }
    }
}

public readonly partial struct ConfigContent : IValueObject<string>
{
    public static void Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ValidationException("Config content cannot be empty.");
        }

        try
        {
            var node = JsonNode.Parse(input);
            if (node is not JsonObject)
            {
                throw new ValidationException("Config content must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            throw new ValidationException("Invalid JSON format.");
        }
    }
}
