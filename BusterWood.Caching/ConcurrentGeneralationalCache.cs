using System;
using System.Collections.Generic;

namespace BusterWood.Caching
{
    /// <summary>A concurrent version of the <see cref="GenerationalCache{TKey, TValue}"/> that uses a number of smaller caches to provide concurrent read and modification</summary>
    public class ConcurrentGeneralationalCache<TKey, TValue> : ICache<TKey, TValue>
    {
        readonly GenerationalCache<TKey, TValue>[] _partitions;
        readonly int _partitionCount;

        /// <summary>Allows a consumer to be notified when a entry has been removed from the cache by one of the <see cref="Invalidate(TKey)"/> methods</summary>
        public event InvalidatedHandler<TKey> Invalidated;

        /// <summary>Create a new cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="timeToLive">(Optional) time period after which a unread item is evicted from the cache</param>
        /// <param name="partitions">The number of paritions to split the cache into, defaults to <see cref="Environment.ProcessorCount"/></param>
        public ConcurrentGeneralationalCache(int? gen0Limit, TimeSpan? timeToLive, int partitions = 0)
        {
            if (partitions == 0)
                partitions = Environment.ProcessorCount;
            else if (partitions < 1)
                throw new ArgumentOutOfRangeException(nameof(partitions), partitions, "Must be one or more");

            _partitionCount = partitions;
            _partitions = new GenerationalCache<TKey, TValue>[partitions];
            for (int i = 0; i < partitions; i++)
            {
                _partitions[i] = new GenerationalCache<TKey, TValue>(gen0Limit / partitions, timeToLive);
                _partitions[i].Invalidated += Partition_Invalidated;
            }
        }

        /// <summary>Bubble up the invalidation event</summary>
        void Partition_Invalidated(object sender, TKey key)
        {
            Invalidated?.Invoke(sender, key);
        }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The <see cref="Maybe.Some{TKey}(TKey)"/> if the item was found in the this cache or the underlying data source, otherwise <see cref="Maybe.None{TKey}"/></returns>
        public Maybe<TValue> Get(TKey key)
        {
            int idx = PartitionIndex(key);
            return _partitions[idx].Get(key);
        }

        /// <summary>Removes a a number of <paramref name="keys" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(IEnumerable<TKey> keys)
        {
            foreach (var k in keys)
                Invalidate(k);
        }

        /// <summary>Removes a <param name="key" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(TKey key)
        {
            int idx = PartitionIndex(key);
            _partitions[idx].Invalidate(key);
        }

        /// <summary>Sets the <paramref name="value"/> associated with a <paramref name="key"/></summary>
        public void Set(TKey key, TValue value)
        {
            int idx = PartitionIndex(key);
            _partitions[idx].Set(key, value);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        int PartitionIndex(TKey key)
        {
            int positiveHashCode = key.GetHashCode() & ~int.MinValue;
            // the following optimizes the modulus (IDIV) instruction for the common cases, as IDIV takes 60-80 clock cycles
            if (_partitionCount == 1)
                return 0;
            if (_partitionCount == 2)
                return positiveHashCode & 3;
            if (_partitionCount == 4)
                return positiveHashCode & 7;
            if (_partitionCount == 8)
                return positiveHashCode & 15;
            if (_partitionCount == 16)
                return positiveHashCode & 31;
            if (_partitionCount == 32)
                return positiveHashCode & 63;
            else
                return positiveHashCode % _partitionCount;            
        }
    }
}
