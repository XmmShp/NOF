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
    }
}
