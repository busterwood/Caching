﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>Prevents calling the underlying data source concurrently *for the same key*.</summary>
    public class ThunderingHurdProtection<TKey, TValue> : ICache<TKey, TValue>
    {
        readonly Dictionary<TKey, TaskCompletionSource<Maybe<TValue>>> loading;
        readonly ICache<TKey, TValue> _dataSource;
        readonly InvalidatedHandler<TKey> _invalidated;

        public ThunderingHurdProtection(ICache<TKey, TValue> dataSource)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            this._dataSource = dataSource;
            loading = new Dictionary<TKey, TaskCompletionSource<Maybe<TValue>>>();
            _invalidated = dataSource_Invalidated;
            dataSource.Invalidated += _invalidated;
        }

        /// <summary>Invalidate this cache when the underlying data source notifies us of an cache invalidation</summary>
        void dataSource_Invalidated(object sender, TKey key)
        {
            Invalidate(key);
        }

        public int Count => 0;

        public event InvalidatedHandler<TKey> Invalidated;

        public void Invalidate(IEnumerable<TKey> keys)
        {
            foreach (var k in keys)
                Invalidate(k);
        }

        public void Invalidate(TKey key)
        {
            Invalidated?.Invoke(this, key);
        }

        public Maybe<TValue> Get(TKey key)
        {
            TaskCompletionSource<Maybe<TValue>> tcs;
            bool alreadyLoading;
            lock (loading)
            {
                alreadyLoading = loading.TryGetOrAdd(key, NewTcs, out tcs);
            }

            if (alreadyLoading)
            {
                // wait for another thread to load the value, then return
                return tcs.Task.Result;   // blocks
            }

            // call the data source to load it
            var maybe = _dataSource.Get(key); // blocks

            tcs.SetResult(maybe); // tell any waiting threads the loaded value
            lock (loading)
            {
                loading.Remove(key);
            }
            return maybe;
        }

        public async Task<Maybe<TValue>> GetAsync(TKey key)
        {
            TaskCompletionSource<Maybe<TValue>> tcs;
            bool alreadyLoading;
            lock (loading)
            {
                alreadyLoading = loading.TryGetOrAdd(key, NewTcs, out tcs);
            }

            if (alreadyLoading)
            {
                return await tcs.Task;
            }

            // call the data source to load it
            var maybe = await _dataSource.GetAsync(key);

            tcs.SetResult(maybe);
            lock (loading)
            {
                loading.Remove(key);
            }
            return maybe;
        }

        static TaskCompletionSource<Maybe<TValue>> NewTcs(TKey key)
        {
            return new TaskCompletionSource<Maybe<TValue>>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            var batch = WorkOutWhatNeedsToBeLoaded(keys);

            if (batch.IsMixedLoad(keys.Count))
                return TryGetBatchMixed(keys, batch);

            // we are loading all the keys
            Maybe<TValue>[] loaded = _dataSource.GetBatch(keys);

            SetAllCompletionSources(batch.TaskCompletionSources, loaded);
            lock (loading)
            {
                loading.RemoveAll(keys);
            }
            return loaded;
        }

        BatchLoad WorkOutWhatNeedsToBeLoaded(IReadOnlyCollection<TKey> keys)
        {
            var tcs = new TaskCompletionSource<Maybe<TValue>>[keys.Count];
            var alreadyLoading = new bool[keys.Count];
            int toLoadCount = 0;
            lock (loading)
            {
                int i = 0;
                foreach (var k in keys)
                {
                    TaskCompletionSource<Maybe<TValue>> t;
                    alreadyLoading[i] = loading.TryGetOrAdd(k, NewTcs, out t);
                    if (alreadyLoading[i])
                        toLoadCount++;
                    tcs[i] = t;
                    i++;
                }
            }
            return new BatchLoad(tcs, alreadyLoading, toLoadCount);
        }

        struct BatchLoad
        {
            public readonly TaskCompletionSource<Maybe<TValue>>[] TaskCompletionSources;
            public readonly bool[] AlreadyLoading;
            public readonly int ToLoadCount;

            public BatchLoad(TaskCompletionSource<Maybe<TValue>>[] tcs, bool[] alreadyLoading, int toLoadCount)
            {
                this.TaskCompletionSources = tcs;
                this.AlreadyLoading = alreadyLoading;
                this.ToLoadCount = toLoadCount;
            }

            public bool IsMixedLoad(int keys) => ToLoadCount < keys;
        }

        static void SetAllCompletionSources(TaskCompletionSource<Maybe<TValue>>[] taskCompletionSources, Maybe<TValue>[] loaded)
        {
            int j = 0;
            foreach (var tcs in taskCompletionSources)
            {
                tcs.SetResult(loaded[j]);
                j++;
            }
        }

        private Maybe<TValue>[] TryGetBatchMixed(IReadOnlyCollection<TKey> keys, BatchLoad batch)
        {
            throw new NotImplementedException();

            // construct a list of keys to load
            var keysToLoad = new List<TKey>(batch.ToLoadCount);
            var originalIndexes = new List<int>(batch.ToLoadCount);
            int j = 0;
            foreach (var key in keys)
            {
                if (!batch.AlreadyLoading[j])
                {
                    keysToLoad.Add(key);
                    originalIndexes.Add(j);
                }
                j++;
            }

            // the following line may block
            Maybe<TValue>[] loaded = keysToLoad.Count > 0 ? _dataSource.GetBatch(keys) : new Maybe<TValue>[0];
        }

        public Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _dataSource.Invalidated -= _invalidated;
        }
    }
}