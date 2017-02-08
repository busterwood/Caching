using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>A cache map that uses generations to cache to minimize the per-key overhead</summary>
    public class GenerationalMap<TKey, TValue> : IReadOnlyMap<TKey, TValue>
    {
        readonly IReadOnlyMap<TKey, TValue> _nextLevel;
        readonly SemaphoreSlim _lock;
        internal Dictionary<TKey, TValue> _gen0;
        internal Dictionary<TKey, TValue> _gen1;
        readonly int _gen0Limit;

        public GenerationalMap(IReadOnlyMap<TKey, TValue> nextLevel, int gen0Limit)
        {
            if (nextLevel == null)
                throw new ArgumentNullException(nameof(nextLevel));
            if (gen0Limit < 1)
                throw new ArgumentOutOfRangeException(nameof(gen0Limit), "Value must be one or more");
            _nextLevel = nextLevel;
            _gen0 = new Dictionary<TKey, TValue>();
            _gen0Limit = gen0Limit;
            _lock = new SemaphoreSlim(1);
        }
        
        /// <summary>Tries to get a value from this cache, or load it from the underlying cache</summary>
        /// <param name="key">Teh key to find</param>
        /// <returns>The value found, or default(T) if not found</returns>
        public TValue Get(TKey key)
        {
            TValue value;
            _lock.Wait();
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
                value = _nextLevel.Get(key); // NOTE: possible blocking

                if (Equals(default(TValue), value)) // NOTE: possible boxing
                    return value; // not found

                // about to add, check the limmit
                if (_gen0.Count >= _gen0Limit)
                {
                    _gen1 = _gen0; // Gen1 items are dropped from the cache at this point
                    _gen0 = new Dictionary<TKey, TValue>(); // Gen0 is now empty, we choose not to re-use Gen1 dictionary so the memory can be GC'd
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
                value = await _nextLevel.GetAsync(key);

                if (default(TValue).Equals(value)) // NOTE: possible boxing
                    return value; // not found

                // about to add, check the limmit
                if (_gen1.Count >= _gen0Limit)
                {
                    _gen1 = _gen0; // Gen1 items are dropped from the cache at this point
                    _gen0 = new Dictionary<TKey, TValue>(); // Gen0 is now empty, we choose not to re-use Gen1 dictionary so the memory can be GC'd
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

    }
}