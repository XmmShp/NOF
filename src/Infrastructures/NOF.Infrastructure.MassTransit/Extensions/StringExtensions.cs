namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureMassTransitExtensions
{
    ///
    extension(string str)
    {
        public Uri ToQueueUri() => new($"queue:{str}");
    }
}
