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
    public class MemoryOverheadTests
    {
        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void generational_cache_memory_overhead(int items)
        {
            string[] keys = CreateKeyStrings(items);
            var starting = GC.GetTotalMemory(true);
            var cache = new GenerationalMap<string, string>(new ValueIsKey<string, string>(), items / 2);
            foreach (var key in keys)
            {
                Assert.AreEqual(key, cache.Get(key));
            }
            var allocated = GC.GetTotalMemory(false) - starting;
            var held = GC.GetTotalMemory(true) - starting;
            Console.WriteLine($"{items} added to cache, {cache._gen0.Count + cache._gen1.Count} item in the cache, allocated {allocated:N0} bytes, holding {held:N0} bytes, overhead per item {held / (double)items:N2} bytes");
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void system_caching_memory_overhead(int items)
        {
            string[] keys = CreateKeyStrings(items);
            var starting = GC.GetTotalMemory(true);
            var cache = new MemoryCache("test");
            foreach (var key in keys)
            {
                cache.Add(key, key, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromSeconds(10) });
            }
            var allocated = GC.GetTotalMemory(false) - starting;
            var held = GC.GetTotalMemory(true) - starting;
            Console.WriteLine($"{items} added to cache, allocated {allocated:N0} bytes, holding {held:N0} bytes, overhead per item {held / (double)items:N2} bytes");
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void simple_dictionary_memory_overhead(int items)
        {
            string[] keys = CreateKeyStrings(items);
            var starting = GC.GetTotalMemory(true);
            var cache = new Dictionary<string, string>();
            foreach (var key in keys)
            {
                cache.Add(key, key);
            }
            var allocated = GC.GetTotalMemory(false) - starting;
            var held = GC.GetTotalMemory(true) - starting;
            Console.WriteLine($"{items} added to cache, allocated {allocated:N0} bytes, holding {held:N0} bytes, overhead per item {held / (double)items:N2} bytes");
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void concurrent_dictionary_memory_overhead(int items)
        {
            string[] keys = CreateKeyStrings(items);
            var starting = GC.GetTotalMemory(true);
            var cache = new ConcurrentDictionary<string, string>();
            foreach (var key in keys)
            {
                cache.TryAdd(key, key);
            }
            var allocated = GC.GetTotalMemory(false) - starting;
            var held = GC.GetTotalMemory(true) - starting;
            Console.WriteLine($"{items} added to cache, allocated {allocated:N0} bytes, holding {held:N0} bytes, overhead per item {held / (double)items:N2} bytes");
        }

        private static string[] CreateKeyStrings(int items)
        {
            string[] keys = new string[items];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = (i + 1).ToString();
            }
            return keys;
        }

    }

}
