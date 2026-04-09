namespace NOF.UI;

public interface ISessionStorage
{
    ValueTask<string?> GetItemAsync(string key);

    ValueTask SetItemAsync(string key, string value);

    ValueTask RemoveItemAsync(string key);
}
