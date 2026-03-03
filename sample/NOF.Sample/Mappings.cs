using NOF.Application;
using NOF.Sample;

namespace ConfigurationCenter;

[Mappable<ConfigFile, ConfigFileDto>]
[Mappable<ConfigNode, ConfigNodeDto>]
public static partial class Mappings;
