using System.Globalization;
using System.Text.Json.Serialization;

namespace NOF.Contract;

public static class PaginatedResult
{
    public const string TotalCountKey = "totalCount";
    public static PaginatedResult<T> Success<T>(IEnumerable<T> value, int? totalCount = null, IDictionary<string, string>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        var resultExtra = extra.CreateOrCopy();
        if (totalCount is int count)
        {
            resultExtra[TotalCountKey] = count.ToString(CultureInfo.InvariantCulture);
        }

        return new PaginatedResult<T>(true, string.Empty, string.Empty, [.. value], resultExtra);
    }
}

/// <summary>
/// Represents the result of a paginated query.
/// </summary>
/// <typeparam name="T">The type of the paginated items.</typeparam>
public sealed record PaginatedResult<T> : Result<T[]>, IResult<PaginatedResult<T>>
{
    [JsonConstructor]
    internal PaginatedResult(bool isSuccess, string? errorCode, string? message, T[]? value, IDictionary<string, string>? extra = null)
        : base(isSuccess, errorCode, message, value, extra)
    {
    }

    /// <summary>
    /// The total number of records across all pages when available.
    /// </summary>
    [JsonIgnore]
    public int? TotalCount => TryGetTotalCount(Extra, out var totalCount) ? totalCount : null;

    public static new PaginatedResult<T> From(IResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is PaginatedResult<T> typed)
        {
            return typed;
        }

        if (!other.IsSuccess)
        {
            return new PaginatedResult<T>(false, other.ErrorCode, other.Message, [], other.Extra);
        }

        int? totalCount = TryGetTotalCount(other.Extra, out var parsedTotalCount) ? parsedTotalCount : null;
        if (other.Value is T[] items)
        {
            return PaginatedResult.Success(items, totalCount, other.Extra);
        }

        if (other.Value is IEnumerable<T> enumerable)
        {
            return PaginatedResult.Success(enumerable, totalCount, other.Extra);
        }

        if (other.Value is T value)
        {
            return PaginatedResult.Success([value], totalCount, other.Extra);
        }

        throw new InvalidOperationException(
            $"Cannot convert a successful '{other.GetType().FullName}' to '{typeof(PaginatedResult<T>).FullName}'.");
    }

    private static bool TryGetTotalCount(IDictionary<string, string>? extra, out int totalCount)
    {
        totalCount = default;
        return extra is not null
            && extra.TryGetValue(PaginatedResult.TotalCountKey, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out totalCount);
    }
}
