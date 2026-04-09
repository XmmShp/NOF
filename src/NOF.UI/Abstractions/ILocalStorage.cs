namespace NOF.UI;

public interface ILocalStorage
{
    ValueTask<string?> GetItemAsync(string key);

    ValueTask SetItemAsync(string key, string value);

    ValueTask RemoveItemAsync(string key);
}
