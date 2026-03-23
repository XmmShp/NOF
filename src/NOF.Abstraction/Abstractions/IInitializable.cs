namespace NOF.Abstraction;

public interface IInitializable
{
    bool IsInitialized { get; }

    void Initialize();
}
