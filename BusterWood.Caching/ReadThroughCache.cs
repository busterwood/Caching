using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>
    /// Dumb base class that can help define implementations of <see cref="IReadThroughCache{TKey, TValue}"/>.  
    /// You must override <see cref="Get(TKey)"/>, and the default implementation of the other get methods just call <see cref="Get(TKey)"/>
    /// </summary>
    public abstract class ReadThroughCache<TKey, TValue> : IReadThroughCache<TKey, TValue>
    {
        public virtual int Count
        {
            get { throw new NotImplementedException(); }
        }

        public event EvictedHandler<TKey, Maybe<TValue>> Evicted;

        public event InvalidatedHandler<TKey> Invalidated;

        public virtual void Dispose()
        {
        }

        public abstract Maybe<TValue> Get(TKey key);

        public virtual Task<Maybe<TValue>> GetAsync(TKey key) => Task.Run(() => Get(key));

        public virtual Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            var results = new Maybe<TValue>[keys.Count];
            int i = 0;
            foreach (var key in keys)
            {
                results[i++] = Get(key);
            }
            return results;
        }

        public async virtual Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            var results = new Maybe<TValue>[keys.Count];
            int i = 0;
            foreach (var key in keys)
            {
                results[i++] = await GetAsync(key);
            }
            return results;
        }

        public virtual void InvalidateAll()
        {
            // nothing to do
        }

        public virtual void Invalidate(IEnumerable<TKey> keys)
        {
            foreach (var k in keys)
                Invalidate(k);
        }

        public virtual void Invalidate(TKey key)
        {
            Invalidated?.Invoke(this, key);
        }
    }
}
