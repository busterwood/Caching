using System;
using System.Collections.Generic;

namespace BusterWood.Caching
{
    public static class CacheExtensions
    {
         /// <summary>Create a new read-through cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="cache">The underlying cache to load data from</param>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="timeToLive">(Optional) time period after which a unread item is evicted from the cache</param>
        public static ReadThroughCache<TKey, TValue> WithGenerationalCache<TKey, TValue>(this IDataSource<TKey, TValue> cache, int? gen0Limit, TimeSpan? timeToLive)
        {
            return new ReadThroughCache<TKey, TValue>(cache, gen0Limit, timeToLive);
        }

        /// <summary>
        /// Adds <see cref="ThunderingHerdProtection{TKey, TValue}"/> to a cache which prevents 
        /// calling the data source concurrently *for the same key*.
        /// </summary>
        /// <param name="cache">The underlying cache to load data from</param>
        public static ThunderingHerdProtection<TKey, TValue> WithThunderingHerdProtection<TKey, TValue>(this IDataSource<TKey, TValue> cache)
        {
            return new ThunderingHerdProtection<TKey, TValue>(cache);
        }

        /// <summary>Gets the existing value in the cache or adds a new value created by the <paramref name="valueFactory"/></summary>
        public static TValue GetOrAdd<TKey, TValue>(this ICache<TKey, TValue> cache, TKey key, Func<TKey, TValue> valueFactory) where TValue : class
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));
            lock (cache.SyncRoot)
            {
                var value = cache[key];
                if (value == null)
                    cache[key] = value = valueFactory(key);
                return value;
            }
        }

        /// <summary>Adds or updates multiple values</summary>
        public static void AddOrUpdateRange<TKey, TValue>(this ICache<TKey, TValue> cache, IEnumerable<KeyValuePair<TKey, TValue>> pairs)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (pairs == null) throw new ArgumentNullException(nameof(pairs));
            lock (cache.SyncRoot)
            {
                foreach (var p in pairs)
                {
                    cache[p.Key] = p.Value;
                }
            }
        }

        /// <summary>Removes multiple keys from the cache</summary>
        public static void RemoveRange<TKey, TValue>(this ICache<TKey, TValue> cache, IEnumerable<TKey> keys)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            lock (cache.SyncRoot)
            {
                foreach (var k in keys)
                {
                    cache.Remove(k);
                }
            }
        }

        /// <summary>Tries to add a value to the cache, returns TRUE if the value was added, FALSE if the cache already contains an value for this key</summary>
        public static bool TryAdd<TKey, TValue>(this ICache<TKey, TValue> cache, TKey key, TValue value) where TValue : class
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (value == null) throw new ArgumentNullException(nameof(value));
            lock (cache.SyncRoot)
            {
                var existing = cache[key];
                if (value == null)
                {
                    cache[key] = value;
                    return true;
                }
                return false;
            }
        }


    }
}