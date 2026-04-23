using System.Text;

namespace NOF.Infrastructure;

public interface IObjectSerializer
{
    ReadOnlyMemory<byte> Serialize(object? value, Type? runtimeType = null);

    object? Deserialize(ReadOnlyMemory<byte> data, Type runtimeType);
}

public static class ObjectSerializerExtensions
{
    extension(IObjectSerializer serializer)
    {
        public T? Deserialize<T>(ReadOnlyMemory<byte> data)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            if (data.IsEmpty)
            {
                return default;
            }

            var obj = serializer.Deserialize(data, typeof(T));
            return obj is null ? default : (T)obj;
        }

        public string SerializeToText(object? value, Type? runtimeType = null)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            var bytes = serializer.Serialize(value, runtimeType);
            return bytes.IsEmpty ? string.Empty : Encoding.UTF8.GetString(bytes.Span);
        }

        public object? Deserialize(string? data, Type runtimeType)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(runtimeType);

            if (string.IsNullOrWhiteSpace(data))
            {
                return default;
            }

            return serializer.Deserialize(Encoding.UTF8.GetBytes(data), runtimeType);
        }

        public T? Deserialize<T>(string? data)
        {
            ArgumentNullException.ThrowIfNull(serializer);

            if (string.IsNullOrWhiteSpace(data))
            {
                return default;
            }

            var obj = serializer.Deserialize(data, typeof(T));
            return obj is null ? default : (T)obj;
        }
    }
}
