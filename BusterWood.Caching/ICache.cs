namespace BusterWood.Caching
{
    /// <summary>A cache you can add item too, read, or <see cref="IInvalidator{TKey}.Invalidate(TKey)"/></summary>
    public interface ICache<TKey, TValue> : IInvalidator<TKey>
    {
        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The <see cref="Maybe.Some{TKey}(TKey)"/> if the item was found in the this cache or the underlying data source, otherwise <see cref="Maybe.None{TKey}"/></returns>
        Maybe<TValue> Get(TKey key);

        /// <summary>Sets the <paramref name="value"/> associated with a <paramref name="key"/></summary>
        void Set(TKey key, TValue value);
    }
}
