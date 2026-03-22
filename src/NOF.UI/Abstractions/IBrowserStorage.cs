namespace NOF.UI;

public interface IBrowserStorage
{
    ValueTask<string?> GetItemAsync(string key);

    ValueTask SetItemAsync(string key, string value);

    ValueTask RemoveItemAsync(string key);
}

public interface ILocalStorage : IBrowserStorage;

public interface ISessionStorage : IBrowserStorage;

