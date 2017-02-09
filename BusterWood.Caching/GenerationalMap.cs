using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>
    /// A cache map that uses generations to cache to minimize the per-key overhead.
    /// A collection releases all items in Gen1 and moves Gen0 -> Gen1.  Reading an item in Gen1 promotes the item back to Gen0.
    /// </summary>
    public class GenerationalMap<TKey, TValue> : IReadOnlyMap<TKey, TValue>, IDisposable
    {
        readonly IReadOnlyMap<TKey, TValue> _dataSource;
        readonly SemaphoreSlim _lock; // the only lock that support "WaitAsync()"
        internal Dictionary<TKey, TValue> _gen0;
        internal Dictionary<TKey, TValue> _gen1;
        readonly IEqualityComparer<TValue> _comparer;
        readonly Task _periodicCollect;
        volatile bool _stop;    // stop the periodic collection
        DateTime _lastCollection; // stops a periodic collection running if a size limit collection as happened since the last periodic GC

        public int? Gen0Limit { get; }
        public TimeSpan? HalfLife { get; }

        /// <summary>Create a new read-through cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="dataSource">The underlying source to load data from</param>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="halfLife">(Optional) time period after which a collection occurs</param>
        public GenerationalMap(IReadOnlyMap<TKey, TValue> dataSource, int? gen0Limit, TimeSpan? halfLife)
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
            _gen0 = new Dictionary<TKey, TValue>();
            Gen0Limit = gen0Limit;
            HalfLife = halfLife;
            _lock = new SemaphoreSlim(1);
            _comparer = EqualityComparer<TValue>.Default;
            if (halfLife != null)
                _periodicCollect = PeriodicCollection(halfLife.Value);
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
            _lock.Wait();
            try
            {
                if (_gen0.TryGetValue(key, out value))
                    return true;

                if (_gen1?.TryGetValue(key, out value) == true)
                {
                    // promote from Gen1 => Gen0
                    _gen1.Remove(key);
                    _gen0.Add(key, value);
                    return true;
                }

                // key not found by this point
                if (!_dataSource.TryGet(key, out value)) // NOTE: possible blocking
                    return false; // not found

                // about to add, check the limit
                if (_gen0.Count >= Gen0Limit)
                {
                    Collect();
                }

                // a new item in the cache
                _gen0.Add(key, value);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">Teh key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public async Task<TValue> GetAsync(TKey key)
        {
            TValue value;
            await _lock.WaitAsync();
            try
            {
                if (_gen0.TryGetValue(key, out value))
                    return value;

                if (_gen1?.TryGetValue(key, out value) == true)
                {
                    // promote from Gen1 => Gen0
                    _gen1.Remove(key);
                    _gen0.Add(key, value);
                    return value;
                }

                // key not found by this point
                value = await _dataSource.GetAsync(key);

                if (_comparer.Equals(default(TValue), value)) // NOTE: possible boxing
                    return value; // not found

                // about to add, check the limit
                if (_gen1.Count >= Gen0Limit)
                {
                    Collect();
                }

                // a new item in the cache
                _gen0.Add(key, value);
                return value;
            }
            finally
            {
                _lock.Release();
            }
        }

        private void Collect()
        {
            // don't create a new dictionary if both are empty
            if (_gen0.Count == 0 && (_gen1?.Count).GetValueOrDefault() == 0)
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

    }
}