using System;
using System.Collections.Generic;

namespace BusterWood.Caching
{
    public class GenerationalCache<TKey, TValue> : GenerationalBase<TKey>, ICache<TKey, TValue>
    {
        internal Dictionary<TKey, TValue> _gen0; // internal for test visibility
        internal Dictionary<TKey, TValue> _gen1; // internal for test visibility

        /// <summary>Create a new cache that has a Gen0 size limit and/or a periodic collection time</summary>
        /// <param name="gen0Limit">(Optional) limit on the number of items allowed in Gen0 before a collection</param>
        /// <param name="timeToLive">(Optional) time period after which a unread item is evicted from the cache</param>
        public GenerationalCache(int? gen0Limit, TimeSpan? timeToLive) : base(gen0Limit, timeToLive)
        {
            _gen0 = new Dictionary<TKey, TValue>();
        }

        public event EvictedHandler<TKey, TValue> Evicted;

        public Maybe<TValue> Get(TKey key)
        {
            TValue value;
            lock (_lock)
            {
                if (_gen0.TryGetValue(key, out value))
                    return Maybe.Some(value);

                if (_gen1?.TryGetValue(key, out value) == true)
                {
                    PromoteGen1ToGen0(key, value);
                    return Maybe.Some(value);
                }
            }
            return Maybe.None<TValue>();
        }

        void PromoteGen1ToGen0(TKey key, TValue value)
        {
            _gen1.Remove(key);
            _gen0.Add(key, value);
        }

        public void Set(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_gen0.ContainsKey(key))
                {
                    // replace existing value in Gen0
                    _gen0[key] = value;
                }
                else if (_gen1?.ContainsKey(key) == true)
                {
                    // Remove key from Gen1, replace with new value in Gen0
                    _gen1.Remove(key);
                    _gen0[key] = value;
                }
                else
                {
                    AddToGen0(key, value);
                }
            }
        }

        void AddToGen0(TKey key, TValue loaded)
        {
            // about to add, check the limit
            if (Gen0LimitReached())
                Collect();

            // a new item in the cache
            _gen0.Add(key, loaded);
        }

        bool Gen0LimitReached() => _gen0.Count >= Gen0Limit;

        protected override int CountCore() => _gen0.Count + (_gen1?.Count).GetValueOrDefault();

        protected override void CollectCore()
        {
            if ((_gen1?.Count).GetValueOrDefault() > 0)
            {
                EvictionCount += _gen1.Count;
                Evicted?.Invoke(this, _gen1);
            }
            _gen1 = _gen0; // Gen1 items are dropped from the cache at this point
            _gen0 = new Dictionary<TKey, TValue>(); // Gen0 is now empty, we choose not to re-use Gen1 dictionary so the memory can be GC'd
        }

        protected override void InvalidateCore(TKey key)
        {
            if (_gen0.Remove(key) || _gen1?.Remove(key) == true)
                OnInvalidated(key);
        }

        public override void InvalidateAll()
        {
            lock (_lock)
            {
                foreach(var key in _gen0.Keys)
                {
                    OnInvalidated(key);
                }
                _gen0.Clear();

                if (_gen1 != null)
                {
                    foreach (var key in _gen1.Keys)
                    {
                        OnInvalidated(key);
                    }
                    _gen1.Clear();
                }
            }
        }
    }
}