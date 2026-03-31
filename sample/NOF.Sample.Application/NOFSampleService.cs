using NOF.Annotation;
using NOF.Application;
using NOF.Sample;

namespace NOF.Sample.Application;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(INOFSampleService) })]
[ServiceImplementation<INOFSampleService>]
public partial class NOFSampleService
{
}
