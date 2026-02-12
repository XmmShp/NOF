using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NOF.Contract;

/// <summary>
/// Base class for PATCH requests that supports partial updates.
/// All JSON properties are captured into <see cref="ExtensionData"/> via a custom converter,
/// enabling distinction between "not sent" (<see cref="Optional.None"/>) and "sent as null" (<c>Optional.Of(null)</c>).
/// <para>
/// Subclasses define typed <see cref="Optional{T}"/> properties using the <see cref="Get{T}"/> and <see cref="Set{T}"/> helpers.
/// These properties are invisible to the JSON serializer — no <c>[JsonIgnore]</c> needed.
/// </para>
/// <example>
/// <code>
/// public record UpdateUserRequest : PatchRequest
/// {
///     public Optional&lt;string&gt; Name
///     {
///         get => Get&lt;string&gt;();
///         set => Set(value);
///     }
///
///     public Optional&lt;int?&gt; Age
///     {
///         get => Get&lt;int?&gt;();
///         set => Set(value);
///     }
/// }
/// </code>
/// </example>
/// </summary>
public abstract record PatchRequest
{
    /// <summary>
    /// The backing store for all JSON properties received in the payload.
    /// Populated by the custom <see cref="PatchRequestConverterFactory"/> during deserialization.
    /// </summary>
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>
    /// Gets an <see cref="Optional{T}"/> value from the extension data dictionary.
    /// Returns <see cref="Optional.None"/> if the key was not present in the JSON payload.
    /// Key lookup follows <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> and
    /// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> from the provided options.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="propertyName">
    /// The CLR property name. Automatically inferred from the caller's member name via <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <param name="options">
    /// Optional <see cref="JsonSerializerOptions"/> for key resolution and deserialization.
    /// Defaults to <see cref="NOFContractExtensions.NOFDefaults"/>.
    /// </param>
    /// <returns>
    /// <see cref="Optional{T}"/> containing the deserialized value if the key was present; otherwise <see cref="Optional.None"/>.
    /// </returns>
    public Optional<T> Get<T>(
        [CallerMemberName] string? propertyName = null,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        if (ExtensionData is null)
        {
            return Optional.None;
        }

        options ??= JsonSerializerOptions.NOFDefaults;

        if (!TryGetElement(propertyName, options, out var element))
        {
            return Optional.None;
        }

        if (element.ValueKind == JsonValueKind.Null)
        {
            return Optional.Of<T>(default!);
        }

        var value = element.Deserialize<T>(options);
        return Optional.Of(value!);
    }

    /// <summary>
    /// Sets an <see cref="Optional{T}"/> value into the extension data dictionary.
    /// If the optional has a value, it is serialized and stored; otherwise the key is removed.
    /// Key resolution follows <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> from the provided options.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The optional value to set.</param>
    /// <param name="propertyName">
    /// The CLR property name. Automatically inferred from the caller's member name via <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <param name="options">
    /// Optional <see cref="JsonSerializerOptions"/> for key resolution and serialization.
    /// Defaults to <see cref="NOFContractExtensions.NOFDefaults"/>.
    /// </param>
    public void Set<T>(
        Optional<T> value,
        [CallerMemberName] string? propertyName = null,
        JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(propertyName);

        options ??= JsonSerializerOptions.NOFDefaults;
        var key = options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;

        if (!value.HasValue)
        {
            ExtensionData?.Remove(key);
            return;
        }

        ExtensionData ??= [];
        ExtensionData[key] = JsonSerializer.SerializeToElement(value.Value, options);
    }

    private bool TryGetElement(string propertyName, JsonSerializerOptions options, out JsonElement element)
    {
        var key = options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
        if (ExtensionData!.TryGetValue(key, out element))
        {
            return true;
        }

        if (!options.PropertyNameCaseInsensitive)
        {
            return false;
        }

        foreach (var kvp in ExtensionData.Where(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            element = kvp.Value;
            return true;
        }

        return false;
    }
}
