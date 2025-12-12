namespace NOF;

public interface IConfiguringServicesConfigurator : IRegistrationConfigurator;
public interface IConfiguredServicesConfigurator : IRegistrationConfigurator, IDepsOn<IConfiguringServicesConfigurator>;

public interface ISyncSeedConfigurator : IStartupConfigurator;
public interface IObservabilityConfigurator : IStartupConfigurator, IDepsOn<ISyncSeedConfigurator>;
public interface ISecurityConfigurator : IStartupConfigurator, IDepsOn<IObservabilityConfigurator>;
public interface IResponseWrapConfigurator : IStartupConfigurator, IDepsOn<ISecurityConfigurator>;
public interface IAuthenticationConfigurator : IStartupConfigurator, IDepsOn<IResponseWrapConfigurator>;
public interface IBusinessConfigurator : IStartupConfigurator, IDepsOn<IAuthenticationConfigurator>;
public interface IEndpointConfigurator : IStartupConfigurator, IDepsOn<IBusinessConfigurator>;