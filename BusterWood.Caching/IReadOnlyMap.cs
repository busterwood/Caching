using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>A key to single value cache interface</summary>
    public interface ICache<TKey, TValue>
    {
        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        TValue Get(TKey key);

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <param name="value">The value found, or default(T) if not found</param>
        /// <returns>TRUE if the item was found in the this cache or the underlying data source, FALSE if no item can be found</returns>
        bool TryGet(TKey key, out TValue value);

        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        Task<TValue> GetAsync(TKey key);

        /// <summary>Tries to get the values associated with the <paramref name="keys"/></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a value or default(T) for each key in the corresponding index</returns>
        TValue[] GetBatch(IReadOnlyCollection<TKey> keys);

        /// <summary>Tries to get the values associated with the <paramref name="keys"/></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a value or default(T) for each key in the corresponding index</returns>
        Task<TValue[]> GetBatchAsync(IReadOnlyCollection<TKey> keys);

        /// <summary>Removes a <param name="key"/> (and value) from the cache, if it exists.</summary>
        void Invalidate(TKey key);

        /// <summary>Removes a <param name="key"/> (and value) from the cache, if it exists.</summary>
        Task InvalidateAsync(TKey key);

        /// <summary>Removes a a number of <paramref name="keys"/> (and value) from the cache, if it exists.</summary>
        void Invalidate(IEnumerable<TKey> keys);

        /// <summary>Removes a a number of <paramref name="keys"/> (and value) from the cache, if it exists.</summary>
        Task InvalidateAsync(IEnumerable<TKey> keys);
    }

    /// <summary>A key to many value cache interface</summary>
    public interface IReadOnlyLookup<TKey, TValue>
    {
        IReadOnlyCollection<TValue> Get(TKey key);
        Task<IReadOnlyCollection<TValue>> GetAsync(TKey key);
    }
}
