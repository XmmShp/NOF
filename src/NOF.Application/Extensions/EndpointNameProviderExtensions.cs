namespace NOF;

public static partial class __NOF_Application_Extensions__
{
    extension(IEndpointNameProvider provider)
    {
        public string GetEndpointName<T>()
            => provider.GetEndpointName(typeof(T));
    }
}
