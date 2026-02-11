namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class NOFInfrastructureCoreExtensions
{
    /// <param name="properties">The dictionary to operate on.</param>
    extension(IDictionary<object, object> properties)
    {
        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, adds a new value produced by the <paramref name="valueFactory"/> and returns it.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up or add.</param>
        /// <param name="valueFactory">A factory function to create the value if the key is not present or the existing value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The existing value (if compatible) or the newly created value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> or <paramref name="valueFactory"/> is null.</exception>
        public T GetOrAdd<T>(object key, Func<object, T> valueFactory)
        {
            if (properties.TryGetValue(key, out var existingValue))
            {
                if (existingValue is T typedValue)
                {
                    return typedValue;
                }
            }

            ArgumentNullException.ThrowIfNull(valueFactory);
            var newValue = valueFactory(key);
            properties[key] = newValue!;
            return newValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, returns the provided <paramref name="defaultValue"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <param name="defaultValue">The default value to return if the key is missing or the value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The value from the dictionary or <paramref name="defaultValue"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is null.</exception>
        public T? GetOrDefault<T>(object key, T? defaultValue = default)
        {
            if (properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, invokes the <paramref name="defaultValueFactory"/> to produce a fallback value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <param name="defaultValueFactory">A factory function to generate a default value if the key is missing or the value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The value from the dictionary or the result of the factory.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> or <paramref name="defaultValueFactory"/> is null.</exception>
        public T GetOrDefault<T>(object key, Func<object, T> defaultValueFactory)
        {
            if (properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            ArgumentNullException.ThrowIfNull(defaultValueFactory);
            return defaultValueFactory(key);
        }
    }

    /// <param name="properties">The dictionary to operate on.</param>
    extension(IDictionary<string, object?> properties)
    {
        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, adds a new value produced by the <paramref name="valueFactory"/> and returns it.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up or add.</param>
        /// <param name="valueFactory">A factory function to create the value if the key is not present or the existing value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The existing value (if compatible) or the newly created value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> or <paramref name="valueFactory"/> is null.</exception>
        public T GetOrAdd<T>(string key, Func<string, T> valueFactory)
        {
            if (properties.TryGetValue(key, out var existingValue))
            {
                if (existingValue is T typedValue)
                {
                    return typedValue;
                }
            }

            ArgumentNullException.ThrowIfNull(valueFactory);
            var newValue = valueFactory(key);
            properties[key] = newValue!;
            return newValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, returns the provided <paramref name="defaultValue"/>.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <param name="defaultValue">The default value to return if the key is missing or the value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The value from the dictionary or <paramref name="defaultValue"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> is null.</exception>
        public T? GetOrDefault<T>(string key, T? defaultValue = default)
        {
            if (properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets the value associated with the specified key if it exists and is of type <typeparamref name="T"/>;
        /// otherwise, invokes the <paramref name="defaultValueFactory"/> to produce a fallback value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="key">The key to look up.</param>
        /// <param name="defaultValueFactory">A factory function to generate a default value if the key is missing or the value is not of type <typeparamref name="T"/>.</param>
        /// <returns>The value from the dictionary or the result of the factory.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="properties"/> or <paramref name="defaultValueFactory"/> is null.</exception>
        public T GetOrDefault<T>(string key, Func<string, T> defaultValueFactory)
        {
            if (properties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            ArgumentNullException.ThrowIfNull(defaultValueFactory);
            return defaultValueFactory(key);
        }
    }
}
