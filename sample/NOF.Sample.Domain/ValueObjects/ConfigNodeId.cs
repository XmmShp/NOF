using Vogen;
using Yitter.IdGenerator;

namespace NOF.Sample;

[ValueObject<long>]
public readonly partial struct ConfigNodeId
{
    public static ConfigNodeId New()
    {
        return From(YitIdHelper.NextId());
    }
}
