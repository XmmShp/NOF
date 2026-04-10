namespace NOF.Infrastructure;

/// <summary>
/// Declares a split-interface RPC service registration on the entry assembly.
/// The Infrastructure source generator consumes this attribute and emits:
/// 1. the composed RPC service implementation,
/// 2. request handler registrations into <see cref="NOF.Application.RequestHandlerRegistry"/>,
/// 3. the service registration into <see cref="NOF.Annotation.AutoInjectRegistry"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SplitInterfaceServiceAttribute<TService, TSplitedInterface> : Attribute
    where TService : class, Contract.IRpcService
    where TSplitedInterface : class, Application.ISplitedInterface<TService>;
