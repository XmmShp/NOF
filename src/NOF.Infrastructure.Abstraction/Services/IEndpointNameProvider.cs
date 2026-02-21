namespace NOF.Infrastructure.Abstraction;

public interface IEndpointNameProvider
{
    string GetEndpointName(Type type);
}
