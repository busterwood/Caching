using BusterWood.Caching;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTests
{
    public class ThunderingHerdProtectionTests
    {
        [Test]
        public void get_reads_though()
        {
            var source = new HerdValueIsKey<int, int>();
            var thp = source.WithThunderingHerdProtection();
            var c = new GenerationalReadThoughCache<int, int>(thp, 10, null);
            Assert.AreEqual(Maybe.Some(1), c.Get(1));
        }
    }

    class HerdValueIsKey<TKey, TValue> : IReadThroughCache<TKey, TValue>
        where TValue : TKey
    {
        public int SpinWaitCount;
        public TimeSpan SleepFor;
        private int ConcurrentGets;

        public int HitCount { get; set; }

        public int Count
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public event InvalidatedHandler<TKey> Invalidated;

        public event EvictedHandler<TKey, Maybe<TValue>> Evicted;

        public Maybe<TValue> Get(TKey key)
        {
            var gets = Interlocked.Increment(ref ConcurrentGets);
            if (gets > 1)
                throw new Exception("Concurrency of more than one");
            if (SpinWaitCount > 0)
                Thread.SpinWait(SpinWaitCount);
            if (SleepFor > TimeSpan.Zero)
                Thread.Sleep(SleepFor + TimeSpan.FromMilliseconds(gets - 1));
            HitCount++;
            Interlocked.Decrement(ref ConcurrentGets);
            return (TValue)key;
        }

        public Task<Maybe<TValue>> GetAsync(TKey key) => Task.FromResult(Maybe.Some((TValue)key));

        public Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            var results = new Maybe<TValue>[keys.Count];
            int i = 0;
            foreach (var k in keys)
            {
                results[i++] = Maybe.Some((TValue)k);
            }
            return results;
        }

        public Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
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

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }
    }

}
