namespace NOF;

public static class OptionalExtensions
{
    extension<T>(Optional<T> optional)
    {
        public T ValueOr(T defaultValue)
            => optional.ValueOr(() => defaultValue);

        public T ValueOr(Func<T> defaultValueFactory)
        {
            return optional.HasValue ? optional.Value : defaultValueFactory();
        }

        public void Match(Action<T> some, Action none)
        {
            if (optional.HasValue)
            {
                some(optional.Value);
            }
            else
            {
                none();
            }
        }

        public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        {
            return optional.HasValue ? some(optional.Value) : none();
        }

        public Optional<TResult> Map<TResult>(Func<T, TResult> valueFactory)
        {
            return optional.HasValue ? Optional.Of(valueFactory(optional.Value)) : Optional.None;
        }

        public TResult? MapAsNullable<TResult>(Func<T, TResult> valueFactory)
        {
            return optional.HasValue ? valueFactory(optional.Value) : default;
        }
    }
}
