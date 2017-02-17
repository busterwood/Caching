using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    public static class CacheExtensions
    {
        /// <summary>Create a new read-through cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="cache">The underlying cache to load data from</param>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="timeToLive">(Optional) time period after which a unread item is evicted from the cache</param>
        public static ICache<TKey, TValue> WithGenerationalCache<TKey, TValue>(this ICache<TKey, TValue> cache, int? gen0Limit, TimeSpan? timeToLive)
        {
            return new GenerationalCache<TKey, TValue>(cache, gen0Limit, timeToLive);
        }

        /// <summary>
        /// Adds <see cref="ThunderingHerdProtection{TKey, TValue}"/> to a cache which prevents 
        /// calling the data source concurrently *for the same key*.
        /// </summary>
        /// <param name="cache">The underlying cache to load data from</param>
        public static ThunderingHerdProtection<TKey, TValue> WithThunderingHerdProtection<TKey, TValue>(this ICache<TKey, TValue> cache)
        {
            return new ThunderingHerdProtection<TKey, TValue>(cache);
        }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <param name="value">The value found, or default(T) if not found</param>
        /// <returns>TRUE if the item was found in the this cache or the underlying data source, FALSE if no item can be found</returns>
        public static bool TryGet<TKey, TValue>(this ICache<TKey, TValue> cache, TKey key, out TValue value)
        {
            var maybe = cache.Get(key);
            value = maybe.GetValueOrDefault();
            return maybe.HasValue;
        }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(this ICache<TKey, TValue> cache, TKey key)
        {
            return cache.Get(key).GetValueOrDefault();
        }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public static async Task<TValue> GetValueOrDefaultAsync<TKey, TValue>(this ICache<TKey, TValue> cache, TKey key)
        {
            var result = await cache.GetAsync(key);
            return result.GetValueOrDefault();
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys"/></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a value or default(T) for each key in the corresponding index</returns>
        public static TValue[] GetBatchValueOrDefault<TKey, TValue>(this ICache<TKey, TValue> cache, IReadOnlyCollection<TKey> keys)
        {
            var maybes = cache.GetBatch(keys);
            return maybes.Select(m => m.GetValueOrDefault()).ToArray();
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys"/></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a value or default(T) for each key in the corresponding index</returns>
        public static async Task<TValue[]> GetBatchValueOfDefaultAsync<TKey, TValue>(this ICache<TKey, TValue> cache, IReadOnlyCollection<TKey> keys)
        {
            var maybes = await cache.GetBatchAsync(keys);
            return maybes.Select(m => m.GetValueOrDefault()).ToArray();
        }
    }
}