using System.Text.Json;
using System.Text.Json.Serialization;

namespace NOF.Contract;

/// <summary>
/// Source-generated JSON serializer context for common types used by the NOF framework.
/// This context provides AOT-safe metadata for primitive types commonly wrapped by
/// value objects, as well as types used internally by framework converters.
/// </summary>
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(long?))]
[JsonSerializable(typeof(short?))]
[JsonSerializable(typeof(byte?))]
[JsonSerializable(typeof(float?))]
[JsonSerializable(typeof(double?))]
[JsonSerializable(typeof(decimal?))]
[JsonSerializable(typeof(bool?))]
[JsonSerializable(typeof(Guid?))]
[JsonSerializable(typeof(DateTime?))]
[JsonSerializable(typeof(DateTimeOffset?))]
[JsonSerializable(typeof(DateOnly?))]
[JsonSerializable(typeof(TimeOnly?))]
[JsonSerializable(typeof(TimeSpan?))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class NOFJsonSerializerContext : JsonSerializerContext;
