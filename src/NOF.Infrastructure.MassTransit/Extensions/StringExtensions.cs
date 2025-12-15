namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class __NOF_Infrastructure_MassTransit_Extensions__
{
    ///
    extension(string str)
    {
        public Uri ToQueueUri() => new($"queue:{str}");
    }
}
