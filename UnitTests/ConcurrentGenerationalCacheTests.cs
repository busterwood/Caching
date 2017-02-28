﻿using NUnit.Framework;
using System;
using BusterWood.Caching;

namespace UnitTests
{
    public class ConcurrentGenerationalCacheTests
    {
        [Test]
        public void empty_cache_has_count_of_zero()
        {
            var c = NewCache();
            Assert.AreEqual(0, c.Count);
        }

        [Test]
        public void can_add_to_cache_and_read_back()
        {
            var c = NewCache();
            c.Set(2, "hello");
            Assert.AreEqual(Maybe.Some("hello"), c.Get(2));
            Assert.AreEqual(1, c.Count, "Count");
        }

        [Test]
        public void can_replace_existing_value()
        {
            var c = NewCache();
            c.Set(2, "hello");
            c.Set(2, "world");
            Assert.AreEqual(Maybe.Some("world"), c.Get(2));
            Assert.AreEqual(1, c.Count, "Count");
        }

        [Test]
        public void can_invalidate()
        {
            var c = NewCache();
            c.Set(2, "hello");
            Assert.AreEqual(1, c.Count, "Count");
            c.Invalidate(2);
            Assert.AreEqual(0, c.Count, "Count");
            Assert.AreEqual(Maybe.None<string>(), c.Get(2));
        }

        [Test]
        public void can_add_and_readback_after_first_collection()
        {
            var c = NewCache();
            c.Set(2, "hello");
            c.ForceCollect();
            Assert.AreEqual(Maybe.Some("hello"), c.Get(2));
            Assert.AreEqual(1, c.Count, "Count");
        }

        [Test]
        public void evicted_after_two_collections()
        {
            var c = NewCache();
            c.Set(2, "hello");
            c.ForceCollect();
            c.ForceCollect();
            Assert.AreEqual(Maybe.None<string>(), c.Get(2));
            Assert.AreEqual(0, c.Count, "Count");
        }

        static ConcurrentGenerationalCache<int, string> NewCache(int gen0 = 10, TimeSpan? ttl = null) => new ConcurrentGenerationalCache<int, string>(gen0, ttl);

    }
}
