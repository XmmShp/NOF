using NOF.Application.Utilities;

namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension(Type type)
    {
        public string GetEndpointName() => EndpointNameProvider.Instance.GetEndpointName(type);
    }
}
