using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>A key to single value cache interface</summary>
    public interface ICache<TKey, TValue> : IInvalidator<TKey>, IDisposable
    {
        int Count { get; }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The <see cref="Maybe.Some{TKey}(TKey)"/> if the item was found in the this cache or the underlying data source, otherwise <see cref="Maybe.None{TKey}"/></returns>
        Maybe<TValue> Get(TKey key);

        /// <summary>Tries to get a value for a key asynchronusly</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The <see cref="Maybe.Some{TKey}(TKey)"/> if the item was found in the this cache or the underlying data source, otherwise <see cref="Maybe.None{TKey}"/></returns>
        Task<Maybe<TValue>> GetAsync(TKey key);

        /// <summary>Tries to get the values associated with the <paramref name="keys"/></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a <see cref="Maybe{T}"/> for each key in the corresponding index</returns>
        Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys);

        /// <summary>Tries to get the values associated with the <paramref name="keys"/> asynchronusly</summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys"/> that contains a <see cref="Maybe{T}"/> for each key in the corresponding index</returns>
        Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys);
    }

    public interface IInvalidator<TKey>
    { 
        /// <summary>Removes a <param name="key"/> (and value) from the cache, if it exists.</summary>
        void Invalidate(TKey key);

        /// <summary>Removes a a number of <paramref name="keys"/> (and value) from the cache, if it exists.</summary>
        void Invalidate(IEnumerable<TKey> keys);

        /// <summary>Allows a consumer to be notified when a entry has been removed from the cache by one of the <see cref="Invalidate(TKey)"/> methods</summary>
        /// <remarks>
        /// This event is *not* called on eviction due to garbage collection.
        /// The implementation of this *should* be implemented using <seealso cref="System.WeakReference"/> so transient caches can be GC'd.
        /// </remarks>
        event InvalidatedHandler<TKey> Invalidated;
    }

    /// <summary>A key to many value cache interface</summary>
    public interface IReadOnlyLookup<TKey, TValue> : IInvalidator<TKey>
    {
        int Count { get; }
        IReadOnlyCollection<TValue> Get(TKey key);
        Task<IReadOnlyCollection<TValue>> GetAsync(TKey key);
    }
}
