using BusterWood.Caching;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTests
{

    class ValueIsKey<TKey, TValue> : ICache<TKey, TValue>
        where TValue : TKey
    {
        public int Count
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event InvalidatedHandler<TKey> Invalidated;

        public TValue Get(TKey key) => (TValue)key;

        public bool TryGet(TKey key, out TValue value)
        {
            value = (TValue)key;
            return true;
        }

        public Task<TValue> GetAsync(TKey key) => Task.FromResult((TValue)key);

        public TValue[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            throw new NotImplementedException();
        }

        public Task<TValue[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            throw new NotImplementedException();
        }

        public void Invalidate(TKey key)
        {
            throw new NotImplementedException();
        }

        public Task InvalidateAsync(TKey key)
        {
            throw new NotImplementedException();
        }

        public void Invalidate(IEnumerable<TKey> keys)
        {
            throw new NotImplementedException();
        }

        public Task InvalidateAsync(IEnumerable<TKey> keys)
        {
            throw new NotImplementedException();
        }
    }
}