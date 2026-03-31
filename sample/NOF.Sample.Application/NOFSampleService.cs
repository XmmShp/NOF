using NOF.Annotation;
using NOF.Application;

namespace NOF.Sample.Application;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(INOFSampleService)])]
[ServiceImplementation<INOFSampleService>]
public partial class NOFSampleService;
