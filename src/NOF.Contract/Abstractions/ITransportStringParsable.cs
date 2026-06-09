namespace NOF.Contract;

/// <summary>
/// Provides parsing from transport string values such as headers and query values.
/// </summary>
/// <typeparam name="TSelf">The implementing value type.</typeparam>
public interface ITransportStringParsable<TSelf>
    where TSelf : ITransportStringParsable<TSelf>
{
    /// <summary>
    /// Tries to parse a transport string value.
    /// </summary>
    /// <param name="value">The string value from the transport.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="result">The parsed value.</param>
    /// <returns>True when parsing succeeds; otherwise false.</returns>
    static abstract bool TryParse(string? value, IFormatProvider? provider, out TSelf result);
}
