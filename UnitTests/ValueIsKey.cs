using BusterWood.Caching;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{

    class ValueIsKey<TKey, TValue> : ICache<TKey, TValue>
        where TValue : TKey
    {
        public int SpinWaitCount;

        public int Count
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event InvalidatedHandler<TKey> Invalidated;

        public TValue Get(TKey key)
        {
            if (SpinWaitCount > 0)
                Thread.SpinWait(SpinWaitCount);
            return (TValue)key;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            if (SpinWaitCount > 0)
                Thread.SpinWait(SpinWaitCount);
            value = (TValue)key;
            return true;
        }

        public Task<TValue> GetAsync(TKey key) => Task.FromResult((TValue)key);

        public TValue[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            var results = new TValue[keys.Count];
            int i = 0;
            foreach (var k in keys)
            {
                results[i++] = (TValue)k;
            }
            return results;
        }

        public Task<TValue[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            return Task.FromResult(GetBatch(keys));
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