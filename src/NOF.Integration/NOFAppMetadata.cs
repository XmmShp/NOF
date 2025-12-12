using System.Diagnostics.CodeAnalysis;

namespace NOF;

/// <summary>
/// Provides a type-safe metadata container for passing contextual information 
/// during the NOF application configuration and startup process.
/// </summary>
public interface INOFAppMetadata
{
    /// <summary>
    /// Sets a metadata value of type <typeparamref name="T"/> under the specified key.
    /// If a value already exists for the key, it will be overwritten.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="name">The unique key identifying the metadata entry. Must not be null.</param>
    /// <param name="value">The value to store. Can be null if <typeparamref name="T"/> is nullable.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    void Set<T>(string name, T value);

    /// <summary>
    /// Retrieves a metadata value of type <typeparamref name="T"/> associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value.</typeparam>
    /// <param name="name">The key of the metadata entry to retrieve. Must not be null.</param>
    /// <returns>The value stored under the given key, cast to type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown if no entry exists for the specified key.</exception>
    /// <exception cref="InvalidCastException">Thrown if the stored value cannot be cast to type <typeparamref name="T"/>.</exception>
    T Get<T>(string name);

    /// <summary>
    /// Attempts to retrieve a metadata value of type <typeparamref name="T"/> associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The expected type of the stored value.</typeparam>
    /// <param name="name">The key of the metadata entry to retrieve. Must not be null.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with the specified key, 
    /// if the key is found and the type matches; otherwise, the default value for type <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key exists and the value can be cast to <typeparamref name="T"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    bool TryGet<T>(string name, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// Determines whether a metadata entry exists for the specified key.
    /// </summary>
    /// <param name="name">The key to check. Must not be null.</param>
    /// <returns><see langword="true"/> if an entry exists for the key; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    bool ContainsKey(string name);

    /// <summary>
    /// Removes the metadata entry associated with the specified key.
    /// </summary>
    /// <param name="name">The key of the entry to remove. Must not be null.</param>
    /// <returns>
    /// <see langword="true"/> if the entry was successfully removed;
    /// <see langword="false"/> if no entry existed for the key.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
    bool Remove(string name);

    /// <summary>
    /// Removes all metadata entries from the container.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets a read-only collection of all metadata keys currently stored.
    /// </summary>
    IReadOnlyCollection<string> Keys { get; }
}
