using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    
    /// <summary>
    /// A cache map that uses generations to cache to minimize the per-key overhead.
    /// A collection releases all items in Gen1 and moves Gen0 -> Gen1.  Reading an item in Gen1 promotes the item back to Gen0.
    /// </summary>
    /// <remarks>This version REMEMBERS cache misses</remarks>
    public class GenerationalCache<TKey, TValue> : ICache<TKey, TValue>
    {
        readonly object _lock; 
        readonly ICache<TKey, TValue> _dataSource;      // the underlying source of values
        internal Dictionary<TKey, Maybe<TValue>> _gen0; // internal for test visibility
        internal Dictionary<TKey, Maybe<TValue>> _gen1; // internal for test visibility
        readonly Task _periodicCollect;
        readonly InvalidatedHandler<TKey> _invalidated; // needed so we can properly dispose of event
        volatile bool _stop;    // stop the periodic collection
        DateTime _lastCollection; // stops a periodic collection running if a size limit collection as happened since the last periodic GC
        int _version;       // used to detect other threads modifying the cache

        /// <summary>the total number of collections</summary>
        public int CollectionCount { get; private set; }

        /// <summary>The total number of item that have been evicted</summary>
        public int EvictionCount { get; private set; }

        /// <summary>(Optional) limit on the number of items allowed in Gen0 before a collection</summary>
        public int? Gen0Limit { get; }

        /// <summary>Period of time after which a unread item is evicted from the cache</summary>
        public TimeSpan? TimetoLive { get; }

        public event EvictedHandler<TKey, Maybe<TValue>> Evicted;

        /// <summary>Create a new read-through cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="dataSource">The underlying source to load data from</param>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="timeToLive">(Optional) time period after which a unread item is evicted from the cache</param>
        public GenerationalCache(ICache<TKey, TValue> dataSource, int? gen0Limit, TimeSpan? timeToLive)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (gen0Limit == null && timeToLive == null)
                throw new ArgumentException("Both gen0Limit and halfLife are not set, at least one must be set");
            if (gen0Limit != null && gen0Limit < 1)
                throw new ArgumentOutOfRangeException(nameof(gen0Limit), "Value must be one or more");
            if (timeToLive != null && timeToLive < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeToLive), "Value must be greater than zero");

            _lock = new object();
            _dataSource = dataSource;
            _gen0 = new Dictionary<TKey, Maybe<TValue>>();
            Gen0Limit = gen0Limit;
            TimetoLive = timeToLive;
            if (timeToLive != null)
                _periodicCollect = PeriodicCollection(timeToLive.Value.TotalMilliseconds / 2);
            _invalidated = dataSource_Invalidated;
            _dataSource.Invalidated += _invalidated;
        }

        /// <summary>Invalidate this cache when the underlying data source notifies us of an cache invalidation</summary>
        void dataSource_Invalidated(object sender, TKey key)
        {
            Invalidate(key);
        }

        /// <summary>The number of items in this cache</summary>
        public int Count
        {
            get
            {
                lock(_lock)
                {
                    return _gen0.Count + (_gen1?.Count).GetValueOrDefault();
                }                
            }
        }

        /// <summary>Tries to get a value for a key</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The <see cref="M:BusterWood.Caching.Maybe.Some``1(``0)" /> if the item was found in the this cache or the underlying data source, otherwise <see cref="M:BusterWood.Caching.Maybe.None``1" /></returns>
        public Maybe<TValue> Get(TKey key)
        {
            Maybe<TValue> result;
            int start;
            lock(_lock)
            {
                start = _version;
                if (TryGetAnyGen(key, out result))
                    return result;
            }            

            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            var loaded =_dataSource.Get(key);

            lock(_lock)
            {
                // another thread may have added the value for our key so double-check
                if (_version != start && TryGetAnyGen(key, out result))
                    return result;

                AddToGen0(key, loaded); // store the value (or the fact that the data source does *not* contain the value
                return loaded;
            }
        }

        /// <summary>Gets the <paramref name="value"/> for a <paramref name="key"/> from Gen0 or Gen1</summary>
        bool TryGetAnyGen(TKey key, out Maybe<TValue> value)
        {
            if (_gen0.TryGetValue(key, out value))
                return true;

            if (_gen1?.TryGetValue(key, out value) == true)
            {
                PromoteGen1ToGen0(key, value);
                return true;
            }
            return false;
        }

        void PromoteGen1ToGen0(TKey key, Maybe<TValue> value)
        {
            _gen1.Remove(key);
            _gen0.Add(key, value);
        }

        void AddToGen0(TKey key, Maybe<TValue> loaded)
        {
            // about to add, check the limit
            if (Gen0LimitReached())
                Collect();

            // a new item in the cache
            _gen0.Add(key, loaded);
            unchecked { _version++; }
        }

        bool Gen0LimitReached() => _gen0.Count >= Gen0Limit;

        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public async Task<Maybe<TValue>> GetAsync(TKey key)
        {
            int start;
            Maybe<TValue> value;
            lock(_lock)
            {
                start = _version;
                if (TryGetAnyGen(key, out value))
                    return value;
            }  
                      
            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            var loaded = await _dataSource.GetAsync(key);

            lock(_lock)
            {
                if (_version != start && TryGetAnyGen(key, out value))
                    return value;

                AddToGen0(key, loaded);
                value = loaded;
                return value;
            }            
        }

        internal void ForceCollect()
        {
            lock(_lock)
            {
                Collect();
            }            
        }

        void Collect()
        {
            CollectionCount++;

            // don't create a new dictionary if both are empty
            if (_gen0.Count == 0 && (_gen1?.Count).GetValueOrDefault() == 0)
                return;
            if (_stop)
                return;
            if ((_gen1?.Count).GetValueOrDefault() > 0)
            {
                EvictionCount += (_gen1?.Count).GetValueOrDefault();
                Evicted?.Invoke(this, _gen1);
            }
            _gen1 = _gen0; // Gen1 items are dropped from the cache at this point
            _gen0 = new Dictionary<TKey, Maybe<TValue>>(); // Gen0 is now empty, we choose not to re-use Gen1 dictionary so the memory can be GC'd
            _lastCollection = DateTime.UtcNow;
        }

        async Task PeriodicCollection(double ms)
        {
            var period = TimeSpan.FromMilliseconds(ms);
            for (;;)
            {
                await Task.Delay(period);
                if (_stop)
                    break;

                lock(_lock)
                {
                    if (_stop)
                        break;

                    if (_lastCollection <= DateTime.UtcNow - period)
                        Collect();
                }                
            }
        }

        public void Dispose()
        {
            _stop = true;
            _dataSource.Invalidated -= _invalidated;
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys" /></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys" /> that contains a value or default(T) for each key in the corresponding index</returns>
        public Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            BatchLoad batch;
            lock (_lock)
            {
                batch = TryGetBatchFromCache(keys);
            }

            // we got all the results from the cache
            if (batch.MissedKeys.Count == 0)
                return batch.Results;

            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            var dsLoaded = _dataSource.GetBatch(batch.MissedKeys);

            return UpdateCacheAndResults(batch, dsLoaded);
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys" /></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys" /> that contains a value or default(T) for each key in the corresponding index</returns>
        public async Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            BatchLoad batch;
            lock (_lock)
            {
                batch = TryGetBatchFromCache(keys);
            }

            // we got all the results from the cache
            if (batch.MissedKeys.Count == 0)
                return batch.Results;

            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            var loaded = await _dataSource.GetBatchAsync(batch.MissedKeys);

            return UpdateCacheAndResults(batch, loaded);
        }

        private BatchLoad TryGetBatchFromCache(IReadOnlyCollection<TKey> keys)
        {
            var batch = new BatchLoad(keys.Count, _version);
            int i = 0;
            foreach (var key in keys)
            {
                Maybe<TValue> value;
                if (TryGetAnyGen(key, out value))
                {
                    batch.Results[i] = value;
                }
                else
                {
                    batch.MissedKeys.Add(key);
                    batch.MissedKeyIdx.Add(i);
                }
                i++;
            }
            return batch;
        }

        struct BatchLoad
        {
            public readonly Maybe<TValue>[] Results;
            public readonly List<TKey> MissedKeys;
            public readonly List<int> MissedKeyIdx;
            public readonly int Version;

            public BatchLoad(int keys, int version)
            {
                Version = version;
                Results = new Maybe<TValue>[keys];
                MissedKeys = new List<TKey>();
                MissedKeyIdx = new List<int>();
            }
        }

        private Maybe<TValue>[] UpdateCacheAndResults(BatchLoad batch, Maybe<TValue>[] fromDataSource)
        {
            lock (_lock)
            {
                int k = 0;
                foreach (var loaded in fromDataSource)
                {
                    if (loaded.HasValue)
                    {
                        var idx = batch.MissedKeyIdx[k];
                        Maybe<TValue> cached;
                        if (batch.Version != _version && TryGetAnyGen(batch.MissedKeys[k], out cached))
                        {
                            // another thread loaded our value
                            batch.Results[idx] = cached;
                            unchecked { _version++; }
                        }
                        else
                        {
                            // we loaded the value, store it in the cache
                            AddToGen0(batch.MissedKeys[k], loaded);
                            batch.Results[idx] = loaded;
                        }
                    }
                    k++;
                }
            }
            return batch.Results;
        }

        /// <summary>Removes a <param name="key" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(TKey key)
        {
            lock(_lock)
            {
                InvalidateKey(key);
            }            
        }

        /// <summary>Removes a a number of <paramref name="keys" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(IEnumerable<TKey> keys)
        {
            lock(_lock)
            {
                foreach (var key in keys)
                {
                    InvalidateKey(key);
                }
            }
        }

        void InvalidateKey(TKey key)
        {
            if (_gen0.Remove(key) || _gen1.Remove(key))
                OnInvalidated(key);
        }

        /// <remarks>Must be called from within the lock</remarks>
        void OnInvalidated(TKey key)
        {
            Invalidated?.Invoke(this, key);
        }

        public event InvalidatedHandler<TKey> Invalidated;
    }
}