using BusterWood.Caching;
using NUnit.Framework;
using System.Collections.Generic;

namespace UnitTests
{
    [TestFixture]
    public class GenerationalReadThroughCacheTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void can_read_item_from_underlying_cache(int key)
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 3, null);
            Assert.AreEqual(key, cache.GetValueOrDefault(key));
        }

        [Test]
        public void moves_items_to_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 4; i++)
            {
                Assert.AreEqual(i, cache.GetValueOrDefault(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

        [Test]
        public void drops_items_in_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 7; i++)
            {
                Assert.AreEqual(i, cache.GetValueOrDefault(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

        [Test]
        public void invalidate_removes_item_from_gen0()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(1, cache.GetValueOrDefault(1));
            Assert.AreEqual(1, cache.Count, "Count");
            cache.Invalidate(1);
            Assert.AreEqual(0, cache.Count, "Count");
        }

        [Test]
        public void invalidate_removes_item_from_gen1()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(1, cache.GetValueOrDefault(1));
            Assert.AreEqual(1, cache.Count, "Count");
            cache.ForceCollect();
            cache.Invalidate(1);
            Assert.AreEqual(0, cache.Count, "Count");
        }

        [Test]
        public void invalidate_raises_event_when_key_in_cache()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            var invalidated = new List<int>();
            Assert.AreEqual(1, cache.GetValueOrDefault(1));
            cache.Invalidated += (sender, key) => invalidated.Add(key);
            cache.Invalidate(1);
            Assert.AreEqual(1, invalidated.Count, "Count");
        }

        [Test]
        public void eviction_raises_event_with_evicted_items()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            IDictionary<int, Maybe<int>> evicted = null;
            Assert.AreEqual(1, cache.GetValueOrDefault(1));
            cache.Evicted += (sender, key) => evicted = key;
            cache.Clear();
            Assert.AreEqual(1, evicted.Count, "Evicted");
            Assert.AreEqual(0, cache.Count, "Cache");
        }

        [Test]
        public void batch_load_reads_from_underlying_datasource_when_key_not_in_cache()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            var results = cache.GetBatchValueOrDefault(new int[] { 2 });
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
        }

        [Test]
        public void batch_load_reads_from_cache()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(2, cache.GetValueOrDefault(2));
            Assert.AreEqual(1, cache.Count, "Count");
            var results = cache.GetBatchValueOrDefault(new int[] { 2 });
            Assert.AreEqual(1, cache.Count, "no extra items added");
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
        }

        [Test]
        public void batch_load_reads_from_cache_and_underlying_datasource()
        {
            var cache = new GenerationalReadThoughCache<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(2, cache.GetValueOrDefault(2));
            Assert.AreEqual(1, cache.Count, "Count");
            var results = cache.GetBatchValueOrDefault(new int[] { 2,3 });
            Assert.AreEqual(2, cache.Count, "no extra items added");
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
            Assert.AreEqual(3, results[1], "results[1]");
        }
    }

}
