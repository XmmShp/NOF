using NOF.Annotation;
using NOF.Application;

namespace NOF.Sample.Application;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(INOFSampleService)])]
public partial class NOFSampleService : ISplitedInterface<INOFSampleService>;
