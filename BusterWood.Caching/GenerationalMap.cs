using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Caching
{

    public delegate void InvalidatedHandler<TKey>(object sender, TKey key);
    
    /// <summary>
    /// A cache map that uses generations to cache to minimize the per-key overhead.
    /// A collection releases all items in Gen1 and moves Gen0 -> Gen1.  Reading an item in Gen1 promotes the item back to Gen0.
    /// </summary>
    public class GenerationalMap<TKey, TValue> : ICache<TKey, TValue>, IDisposable
    {
        
        readonly ICache<TKey, TValue> _dataSource;
        readonly SemaphoreSlim _lock; // the only lock that support "WaitAsync()"
        internal Dictionary<TKey, TValue> _gen0;
        internal Dictionary<TKey, TValue> _gen1;
        readonly IEqualityComparer<TValue> _valueComparer;
        readonly Task _periodicCollect;        
        volatile bool _stop;    // stop the periodic collection
        DateTime _lastCollection; // stops a periodic collection running if a size limit collection as happened since the last periodic GC
        int _version;

        public int? Gen0Limit { get; }
        public TimeSpan? HalfLife { get; }

        /// <summary>Create a new read-through cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="dataSource">The underlying source to load data from</param>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="halfLife">(Optional) time period after which a collection occurs</param>
        public GenerationalMap(ICache<TKey, TValue> dataSource, int? gen0Limit, TimeSpan? halfLife)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            if (gen0Limit == null && halfLife == null)
                throw new ArgumentException("Both gen0Limit and halfLife are not set, at least one must be set");
            if (gen0Limit != null && gen0Limit < 1)
                throw new ArgumentOutOfRangeException(nameof(gen0Limit), "Value must be one or more");
            if (halfLife != null && halfLife < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(halfLife), "Value must be greater than zero");

            _dataSource = dataSource;
            _valueComparer = EqualityComparer<TValue>.Default;
            _gen0 = new Dictionary<TKey, TValue>();
            Gen0Limit = gen0Limit;
            HalfLife = halfLife;
            _lock = new SemaphoreSlim(1);
            if (halfLife != null)
                _periodicCollect = PeriodicCollection(halfLife.Value);
            _dataSource.Invalidated += dataSource_Invalidated;
        }

        void dataSource_Invalidated(object sender, TKey key)
        {
            Invalidate(key);
        }

        public int Count
        {
            get
            {
                _lock.Wait();
                try
                {
                    return _gen0.Count + (_gen1?.Count).GetValueOrDefault();
                }
                finally
                {
                    _lock.Release();
                }
            }
        }


        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public TValue Get(TKey key)
        {
            TValue value;
            TryGet(key, out value);
            return value;
        }

        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">Teh key to find</param>
        /// <param name="value">The value found, or default(T) if not found</param>
        /// <returns>TRUE if the item was found in the this cache or the underlying data source, FALSE if no item can be found</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            int start;
            _lock.Wait();
            try
            {
                start = _version;
                if (_gen0.TryGetValue(key, out value))
                    return true;

                if (_gen1?.TryGetValue(key, out value) == true)
                {
                    PromoteGen1ToGen0(key, value);
                    return true;
                }
            }
            finally
            {
                _lock.Release();
            }

            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            TValue dsValue;
            if (!_dataSource.TryGet(key, out dsValue))
                return false;

            _lock.Wait();
            try
            {
                if (_version != start)
                {
                    // another thread may have added the value for our key so double-check
                    if (_gen0.TryGetValue(key, out value))
                        return true;

                    if (_gen1?.TryGetValue(key, out value) == true)
                    {
                        PromoteGen1ToGen0(key, value);
                        return true;
                    }
                }

                // about to add, check the limit
                if (Gen0LimitReached())
                    Collect();

                // a new item in the cache
                _gen0.Add(key, dsValue);
                value = dsValue;
                unchecked { _version++; }
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        bool Gen0LimitReached() => _gen0.Count >= Gen0Limit;

        void PromoteGen1ToGen0(TKey key, TValue value)
        {
            _gen1.Remove(key);
            _gen0.Add(key, value);
        }

        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">The key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public async Task<TValue> GetAsync(TKey key)
        {
            int start;
            TValue value;
            await _lock.WaitAsync();
            try
            {
                start = _version;
                if (_gen0.TryGetValue(key, out value))
                    return value;

                if (_gen1?.TryGetValue(key, out value) == true)
                {
                    PromoteGen1ToGen0(key, value);
                    return value;
                }
            }
            finally
            {
                _lock.Release();
            }

            // key not found by this point, read-through to the data source *outside* of the lock as this may take some time, i.e. network or file access
            TValue dsValue = await _dataSource.GetAsync(key);
            if (_valueComparer.Equals(default(TValue), dsValue))
                return default(TValue);

            await _lock.WaitAsync();
            try
            {
                if (_version != start)
                {
                    // another thread may have added the value for our key so double-check
                    if (_gen0.TryGetValue(key, out value))
                        return value;

                    if (_gen1?.TryGetValue(key, out value) == true)
                    {
                        PromoteGen1ToGen0(key, value);
                        return value;
                    }
                }

                // about to add, check the limit
                if (Gen0LimitReached())
                    Collect();

                // a new item in the cache
                _gen0.Add(key, dsValue);
                value = dsValue;
                unchecked { _version++; }
                return value;
            }
            finally
            {
                _lock.Release();
            }
        }

        internal void ForceCollect()
        {
            _lock.Wait();
            try
            {
                Collect();
            }
            finally
            {
                _lock.Release();
            }
        }

        void Collect()
        {
            // don't create a new dictionary if both are empty
            if (_gen0.Count == 0 && (_gen1?.Count).GetValueOrDefault() == 0)
                return;
            if (_stop)
                return;
            _gen1 = _gen0; // Gen1 items are dropped from the cache at this point
            _gen0 = new Dictionary<TKey, TValue>(); // Gen0 is now empty, we choose not to re-use Gen1 dictionary so the memory can be GC'd
            _lastCollection = DateTime.UtcNow;
        }

        async Task PeriodicCollection(TimeSpan period)
        {
            for (;;)
            {
                await Task.Delay(period);
                if (_stop)
                    break;

                await _lock.WaitAsync();
                try
                {
                    if (_stop)
                        break;

                    if (_lastCollection <= DateTime.UtcNow - period)
                        Collect();
                }
                finally
                {
                    _lock.Release();
                }
            }
        }

        public void Dispose()
        {
            _stop = true;
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys" /></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys" /> that contains a value or default(T) for each key in the corresponding index</returns>
        public TValue[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            _lock.Wait();
            try
            {
                var results = new TValue[keys.Count];
                var missedKeys = new List<TKey>();
                var missedKeyIdx = new List<int>();
                int i = 0;
                foreach (var key in keys)
                {
                    TValue value;

                    if (_gen0.TryGetValue(key, out value))
                    {
                        results[i] = value;
                    }
                    else if (_gen1?.TryGetValue(key, out value) == true)
                    {
                        PromoteGen1ToGen0(key, value);
                        results[i] = value;
                    }
                    else
                    {
                        missedKeys.Add(key);
                        missedKeyIdx.Add(i);
                    }
                    i++;
                }

                if (missedKeys.Count > 0)
                {
                    var loaded = _dataSource.GetBatch(missedKeys); // NOTE: possible blocking
                    if (_gen0.Count + loaded.Length > Gen0Limit)
                        Collect();

                    int k = 0;
                    foreach (var value in loaded)
                    {
                        _gen0.Add(missedKeys[k], value);
                        var idx = missedKeyIdx[k];
                        results[idx] = value;
                        k++;
                    }
                }

                return results;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Tries to get the values associated with the <paramref name="keys" /></summary>
        /// <param name="keys">The keys to find</param>
        /// <returns>An array the same size as the input <paramref name="keys" /> that contains a value or default(T) for each key in the corresponding index</returns>
        public async Task<TValue[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            await _lock.WaitAsync();
            try
            {
                var results = new TValue[keys.Count];
                var missedKeys = new List<TKey>();
                var missedKeyIdx = new List<int>();
                int i = 0;
                foreach (var key in keys)
                {
                    TValue value;

                    if (_gen0.TryGetValue(key, out value))
                    {
                        results[i] = value;
                    }
                    else if (_gen1?.TryGetValue(key, out value) == true)
                    {
                        PromoteGen1ToGen0(key, value);
                        results[i] = value;
                    }
                    else
                    {
                        missedKeys.Add(key);
                        missedKeyIdx.Add(i);
                    }
                    i++;
                }

                if (missedKeys.Count > 0)
                {
                    var loaded = await _dataSource.GetBatchAsync(missedKeys);
                    if (_gen0.Count + loaded.Length > Gen0Limit)
                        Collect();

                    int k = 0;
                    foreach (var value in loaded)
                    {
                        _gen0.Add(missedKeys[k], value);
                        var idx = missedKeyIdx[k];
                        results[idx] = value;
                        k++;
                    }
                }

                return results;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Removes a <param name="key" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(TKey key)
        {
            _lock.Wait();
            try
            {
                InvalidateKey(key);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Removes a <param name="key" /> (and value) from the cache, if it exists.</summary>
        public async Task InvalidateAsync(TKey key)
        {
            await _lock.WaitAsync();
            try
            {
                InvalidateKey(key);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Removes a a number of <paramref name="keys" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(IEnumerable<TKey> keys)
        {
            _lock.Wait();
            try
            {
                InvalidateKeys(keys);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Removes a a number of <paramref name="keys" /> (and value) from the cache, if it exists.</summary>
        public async Task InvalidateAsync(IEnumerable<TKey> keys)
        {
            await _lock.WaitAsync();
            try
            {
                InvalidateKeys(keys);
            }
            finally
            {
                _lock.Release();
            }
        }

        void InvalidateKeys(IEnumerable<TKey> keys)
        {
            foreach (var key in keys)
            {
                InvalidateKey(key);
            }
            //TODO: batch invalidation event?
        }

        void InvalidateKey(TKey key)
        {
            if (_gen0.Remove(key) || _gen1.Remove(key))
                OnInvalidated(key);
        }

        /// <remarks>Must be within a lock</remarks>
        void OnInvalidated(TKey key)
        {
            Invalidated?.Invoke(this, key);
        }

        public event InvalidatedHandler<TKey> Invalidated;
    }

}