namespace NOF.Abstraction;

public static partial class NOFAbstractionExtensions
{
    extension(Type type)
    {
        public string DisplayName => type.FullName ?? type.Name;
    }
}
