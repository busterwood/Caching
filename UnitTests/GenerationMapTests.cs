using BusterWood.Caching;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class GenerationMapTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void can_read_item_from_underlying_cache(int key)
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            Assert.AreEqual(key, cache.Get(key));
        }

        [Test]
        public void moves_items_to_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 4; i++)
            {
                Assert.AreEqual(i, cache.Get(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

        [Test]
        public void drops_items_in_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 7; i++)
            {
                Assert.AreEqual(i, cache.Get(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

    }

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
