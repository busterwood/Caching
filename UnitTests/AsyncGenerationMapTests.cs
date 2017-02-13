using BusterWood.Caching;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestFixture]
    public class AsyncGenerationMapTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public async Task can_read_item_from_underlying_cache(int key)
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            Assert.AreEqual(key, await cache.GetAsync(key));
        }

        [Test]
        public async Task moves_items_to_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 4; i++)
            {
                Assert.AreEqual(i, await cache.GetAsync(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

        [Test]
        public async Task drops_items_in_gen1_when_gen0_is_full()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 3, null);
            for(int i = 1; i <= 7; i++)
            {
                Assert.AreEqual(i, await cache.GetAsync(i));
            }
            Assert.AreEqual(3, cache._gen1.Count, "gen1.Count");
            Assert.AreEqual(1, cache._gen0.Count, "gen0.Count");
        }

        [Test]
        public async Task batch_load_reads_from_underlying_datasource_when_key_not_in_cache()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            var results = await cache.GetBatchAsync(new int[] { 2 });
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
        }

        [Test]
        public async Task batch_load_reads_from_cache()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(2, cache.Get(2));
            Assert.AreEqual(1, cache.Count, "Count");
            var results = await cache.GetBatchAsync(new int[] { 2 });
            Assert.AreEqual(1, cache.Count, "no extra items added");
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
        }

        [Test]
        public async Task batch_load_reads_from_cache_and_underlying_datasource()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(2, cache.Get(2));
            Assert.AreEqual(1, cache.Count, "Count");
            var results = await cache.GetBatchAsync(new int[] { 2,3 });
            Assert.AreEqual(2, cache.Count, "no extra items added");
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Length, "number of results returned");
            Assert.AreEqual(2, results[0], "results[0]");
            Assert.AreEqual(3, results[1], "results[1]");
        }
    }

}
