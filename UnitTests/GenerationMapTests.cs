using BusterWood.Caching;
using NUnit.Framework;
using System.Collections.Generic;

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

        [Test]
        public void invalidate_removes_item_from_gen0()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(1, cache.Get(1));
            Assert.AreEqual(1, cache.Count, "Count");
            cache.Invalidate(1);
            Assert.AreEqual(0, cache.Count, "Count");
        }

        [Test]
        public void invalidate_removes_item_from_gen1()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            Assert.AreEqual(1, cache.Get(1));
            Assert.AreEqual(1, cache.Count, "Count");
            cache.ForceCollect();
            cache.Invalidate(1);
            Assert.AreEqual(0, cache.Count, "Count");
        }


        [Test]
        public void invalidate_raises_event_when_key_in_cache()
        {
            var cache = new GenerationalMap<int, int>(new ValueIsKey<int, int>(), 10, null);
            var invalidated = new List<int>();
            Assert.AreEqual(1, cache.Get(1));
            cache.Invalidated += (sender, key) => invalidated.Add(key);
            cache.Invalidate(1);
            Assert.AreEqual(1, invalidated.Count, "Count");
        }
    }

}
