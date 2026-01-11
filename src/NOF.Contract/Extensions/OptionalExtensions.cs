namespace NOF;

// ReSharper disable once InconsistentNaming
public static partial class __NOF_Contract_Extensions__
{
    ///
    extension<T>(Optional<T> optional)
    {
        /// <summary>
        /// Returns the contained value if present; otherwise, returns the specified default value.
        /// </summary>
        /// <param name="defaultValue">The value to return if no value is present.</param>
        /// <returns>The contained value or <paramref name="defaultValue"/>.</returns>
        public T ValueOr(T defaultValue)
            => optional.ValueOr(() => defaultValue);

        /// <summary>
        /// Returns the contained value if present; otherwise, invokes the factory function to produce a default value.
        /// This overload avoids unnecessary evaluation of the default when a value is present.
        /// </summary>
        /// <param name="defaultValueFactory">A function that produces a default value when needed.</param>
        /// <returns>The contained value or the result of <paramref name="defaultValueFactory"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="defaultValueFactory"/> is <c>null</c>.
        /// </exception>
        public T ValueOr(Func<T> defaultValueFactory)
        {
            if (optional.HasValue)
            {
                return optional.Value;
            }

            ArgumentNullException.ThrowIfNull(defaultValueFactory);
            return defaultValueFactory();
        }

        /// <summary>
        /// Executes one of two actions based on whether a value is present.
        /// </summary>
        /// <param name="some">Action to execute if a value is present. Receives the value as input.</param>
        /// <param name="none">Action to execute if no value is present.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="some"/> or <paramref name="none"/> is <c>null</c>.
        /// </exception>
        public void Match(Action<T> some, Action none)
        {
            if (optional.HasValue)
            {
                ArgumentNullException.ThrowIfNull(some);
                some(optional.Value);
            }
            else
            {
                ArgumentNullException.ThrowIfNull(none);
                none();
            }
        }

        /// <summary>
        /// Executes an action if a value is present.
        /// </summary>
        /// <param name="action">Action to execute with the value if present.</param>
        /// <returns>The original optional for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="action"/> is <c>null</c>.
        /// </exception>
        public void IfSome(Action<T> action)
            => optional.Match(action, () => { });

        /// <summary>
        /// Executes an action if no value is present.
        /// </summary>
        /// <param name="action">Action to execute if no value is present.</param>
        /// <returns>The original optional for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="action"/> is <c>null</c>.
        /// </exception>
        public void IfNone(Action action)
            => optional.Match(_ => { }, action);

        /// <summary>
        /// Transforms the optional value into a result of type <typeparamref name="TResult"/>.
        /// If a value is present, applies the <paramref name="some"/> function; otherwise, uses the <paramref name="none"/> function.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="some">Function to apply if a value is present.</param>
        /// <param name="none">Function to apply if no value is present.</param>
        /// <returns>The result of applying the appropriate function.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="some"/> or <paramref name="none"/> is <c>null</c>.
        /// </exception>
        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        {
            if (optional.HasValue)
            {
                ArgumentNullException.ThrowIfNull(some);
                return some(optional.Value);
            }

            ArgumentNullException.ThrowIfNull(none);
            return none();
        }

        /// <summary>
        /// Transforms the contained value using the specified function, returning a new <see cref="Optional{TResult}"/>.
        /// If no value is present, returns <see cref="Optional.None"/>.
        /// </summary>
        /// <typeparam name="TResult">The type of the result value.</typeparam>
        /// <param name="valueFactory">The function to apply to the contained value.</param>
        /// <returns>
        /// An <see cref="Optional{TResult}"/> containing the transformed value, or <see cref="Optional.None"/> if no value was present.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="valueFactory"/> is <c>null</c>.
        /// </exception>
        public Optional<TResult> Map<TResult>(Func<T, TResult> valueFactory)
        {
            if (!optional.HasValue)
            {
                return Optional.None;
            }

            ArgumentNullException.ThrowIfNull(valueFactory);
            return Optional.Of(valueFactory(optional.Value));
        }

        /// <summary>
        /// Transforms the contained value into a nullable reference or value type.
        /// If a value is present, returns the result of the transformation; otherwise, returns <c>default</c> (typically <c>null</c> for reference types).
        /// </summary>
        /// <typeparam name="TResult">The type of the result, typically a reference type or nullable value type.</typeparam>
        /// <param name="valueFactory">The function to apply to the contained value.</param>
        /// <returns>
        /// The transformed value if present; otherwise, <c>default(TResult)</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="valueFactory"/> is <c>null</c>.
        /// </exception>
        public TResult? MapAsNullable<TResult>(Func<T, TResult> valueFactory)
        {
            if (!optional.HasValue)
            {
                return default;
            }

            ArgumentNullException.ThrowIfNull(valueFactory);
            return valueFactory(optional.Value);
        }
    }
}
