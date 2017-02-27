﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    public abstract class GenerationalBase<TKey> : IInvalidator<TKey>
    {
        protected readonly object _lock;
        readonly Task _periodicCollect;
        volatile bool _stop;    // stop the periodic collection
        DateTime _lastCollection; // stops a periodic collection running if a size limit collection as happened since the last periodic GC

        /// <summary>the total number of collections</summary>
        public int CollectionCount { get; private set; }

        /// <summary>The total number of item that have been evicted</summary>
        public int EvictionCount { get; protected set; }

        /// <summary>(Optional) limit on the number of items allowed in Gen0 before a collection</summary>
        public int? Gen0Limit { get; }

        /// <summary>Period of time after which a unread item is evicted from the cache</summary>
        public TimeSpan? TimetoLive { get; }

        /// <summary>The number of items in this cache</summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return CountCore();
                }
            }
        }

        public event InvalidatedHandler<TKey> Invalidated;

        public GenerationalBase(int? gen0Limit, TimeSpan? timeToLive)
        {
            if (gen0Limit == null && timeToLive == null)
                throw new ArgumentException("Both gen0Limit and halfLife are not set, at least one must be set");
            if (gen0Limit != null && gen0Limit < 1)
                throw new ArgumentOutOfRangeException(nameof(gen0Limit), "Value must be one or more");
            if (timeToLive != null && timeToLive < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeToLive), "Value must be greater than zero");

            _lock = new object();
            Gen0Limit = gen0Limit;
            TimetoLive = timeToLive;
            if (timeToLive != null)
                _periodicCollect = PeriodicCollection(timeToLive.Value.TotalMilliseconds / 2);
        }

        /// <summary>Collect now and again</summary>
        async Task PeriodicCollection(double ms)
        {
            var period = TimeSpan.FromMilliseconds(ms);
            for (;;)
            {
                await Task.Delay(period);
                if (_stop)
                    break;

                lock (_lock)
                {
                    if (_stop)
                        break;

                    if (_lastCollection <= DateTime.UtcNow - period)
                        Collect();
                }
            }
        }

        internal void ForceCollect()
        {
            lock (_lock)
            {
                Collect();
            }
        }

        protected void Collect()
        {
            if (_stop || CountCore() == 0)
                return;

            CollectCore();
            CollectionCount++;
            _lastCollection = DateTime.UtcNow;
        }

        protected abstract void CollectCore();

        /// <summary>Gets the number of items currently being cached</summary>
        protected abstract int CountCore();

        public virtual void Dispose()
        {
            _stop = true;
        }

        /// <summary>Removes a <param name="key" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(TKey key)
        {
            lock (_lock)
            {
                InvalidateCore(key);
            }
        }

        /// <summary>Removes a a number of <paramref name="keys" /> (and value) from the cache, if it exists.</summary>
        public void Invalidate(IEnumerable<TKey> keys)
        {
            lock (_lock)
            {
                foreach (var key in keys)
                {
                    InvalidateCore(key);
                }
            }
        }

        protected abstract void InvalidateCore(TKey key);

        /// <remarks>Must be called from within the lock</remarks>
        protected void OnInvalidated(TKey key)
        {
            Invalidated?.Invoke(this, key);
        }
    }
}