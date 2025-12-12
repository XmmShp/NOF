namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension(Type type)
    {
        public string GetEndpointName() => IEndpointNameProvider.Instance.GetEndpointName(type);
    }
}
