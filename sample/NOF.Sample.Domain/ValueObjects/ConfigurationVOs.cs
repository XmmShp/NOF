using System.Text.Json;
using System.Text.Json.Nodes;
using Vogen;

namespace NOF.Sample;

[ValueObject<string>]
public readonly partial struct ConfigNodeName
{
    private static string NormalizeInput(string input) => input.Trim();

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Validation.Invalid("Node name cannot be empty.");
        }

        if (input.Length > 100)
        {
            return Validation.Invalid("Node name cannot exceed 100 characters.");
        }

        return Validation.Ok;
    }
}

[ValueObject<string>]
public readonly partial struct ConfigFileName
{
    private static string NormalizeInput(string input) => input.Trim();

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Validation.Invalid("File name cannot be empty.");
        }

        if (input.Length > 100)
        {
            return Validation.Invalid("File name cannot exceed 100 characters.");
        }

        return Validation.Ok;
    }
}

[ValueObject<string>]
public readonly partial struct ConfigContent
{
    private static string NormalizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "{}";

        try
        {
            using var doc = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return input;
        }
    }

    private static Validation Validate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Validation.Invalid("Config content cannot be empty.");
        }

        try
        {
            var node = JsonNode.Parse(input);
            if (node is not JsonObject)
            {
                return Validation.Invalid("Config content must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            return Validation.Invalid("Invalid JSON format.");
        }

        return Validation.Ok;
    }
}

