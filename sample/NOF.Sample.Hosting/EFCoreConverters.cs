using Vogen;

namespace NOF.Sample;

[EfCoreConverter<ConfigNodeId>]
[EfCoreConverter<ConfigNodeName>]
[EfCoreConverter<ConfigFileName>]
[EfCoreConverter<ConfigContent>]
internal partial class EfCoreConverters;