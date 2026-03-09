using NOF.Contract;

namespace NOF.Infrastructure.Abstraction;

public interface IMessageSerializer : IObjectSerializer
{
    string Serialize(IMessage message);

    IMessage Deserialize(string payloadType, string payload);
}
