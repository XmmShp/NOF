namespace NOF.Contract.Extension.Authentication;

public readonly record struct BearerToken(string Value) : ITransportStringParsable<BearerToken>
{
    public static bool TryParse(string? value, IFormatProvider? provider, out BearerToken result)
    {
        var token = value?.Trim() ?? string.Empty;
        const string prefix = "Bearer ";
        if (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[prefix.Length..].TrimStart();
        }

        result = new BearerToken(token);
        return !string.IsNullOrWhiteSpace(token);
    }

    public override string ToString() => Value;
}
