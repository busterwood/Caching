using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>
    /// Used to prevent multiple threads calling an underlying database, or remote service, to load the value for the *same* key.
    /// Different keys are handled concurrently, but indiviual keys are read by only one thread.
    /// </summary>
    /// <remarks>This could be useful on a client, or on the server side</remarks>
    public class ThunderingHerdProtection<TKey, TValue> : IReadThroughCache<TKey, TValue>
    {
        readonly Dictionary<TKey, TaskCompletionSource<Maybe<TValue>>> _loading; // only populated keys currently being read
        readonly IReadThroughCache<TKey, TValue> _dataSource;
        readonly InvalidatedHandler<TKey> _dataSourceInvalidated;
        readonly EvictedHandler<TKey, Maybe<TValue>> _dataSourceEvicted;

        public ThunderingHerdProtection(IReadThroughCache<TKey, TValue> dataSource)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));
            _dataSource = dataSource;
            _loading = new Dictionary<TKey, TaskCompletionSource<Maybe<TValue>>>();
            _dataSourceInvalidated = DataSource_Invalidated;
            dataSource.Invalidated += _dataSourceInvalidated;
            _dataSourceEvicted = DataSource_Evicted;
            dataSource.Evicted += _dataSourceEvicted;
        }

        /// <summary>Invalidate this cache when the underlying data source notifies us of an cache invalidation</summary>
        void DataSource_Invalidated(object sender, TKey key)
        {
            Invalidate(key);
        }

        void DataSource_Evicted(object sender, IDictionary<TKey, Maybe<TValue>> evicted)
        {
            Evicted?.Invoke(this, evicted);
        }

        public int Count => 0;

        public event InvalidatedHandler<TKey> Invalidated;

        public event EvictedHandler<TKey, Maybe<TValue>> Evicted; // not used

        public void Clear()
        {
            _dataSource.Clear();
        }

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
            lock (_loading)
            {
                alreadyLoading = _loading.TryGetOrAdd(key, NewTcs, out tcs);
            }

            if (alreadyLoading)
            {
                // wait for another thread to load the value, then return
                return tcs.Task.Result;   // blocks
            }

            // call the data source to load it
            var maybe = _dataSource.Get(key); // blocks

            Complete(tcs, maybe);
            lock (_loading)
            {
                _loading.Remove(key);
            }
            return maybe;
        }

        static void Complete(TaskCompletionSource<Maybe<TValue>> tcs, Maybe<TValue> maybe)
        {
            // create the TCS with Run Continuations Async on 4.6
            Task.Run(() => tcs.SetResult(maybe)); // tell any waiting threads the loaded value
        }

        public async Task<Maybe<TValue>> GetAsync(TKey key)
        {
            TaskCompletionSource<Maybe<TValue>> tcs;
            bool alreadyLoading;
            lock (_loading)
            {
                alreadyLoading = _loading.TryGetOrAdd(key, NewTcs, out tcs);
            }

            if (alreadyLoading)
            {
                return await tcs.Task;
            }

            // call the data source to load it
            var maybe = await _dataSource.GetAsync(key);

            Complete(tcs, maybe);
            lock (_loading)
            {
                _loading.Remove(key);
            }
            return maybe;
        }

        public Maybe<TValue>[] GetBatch(IReadOnlyCollection<TKey> keys)
        {
            var batch = WorkOutWhatNeedsToBeLoaded(keys);

            if (batch.IsMixedLoad(keys.Count))
                return TryGetBatchMixed(keys, batch);

            // we are loading all the keys
            Maybe<TValue>[] loaded = _dataSource.GetBatch(keys);

            SetAllCompletionSources(batch.TaskCompletionSources, loaded);
            RemoveAllKeys(keys);
            return loaded;
        }

        void RemoveAllKeys(IReadOnlyCollection<TKey> keys)
        {
            lock (_loading)
            {
                _loading.RemoveRange(keys);
            }
        }

        BatchLoad WorkOutWhatNeedsToBeLoaded(IReadOnlyCollection<TKey> keys)
        {
            var tcs = new TaskCompletionSource<Maybe<TValue>>[keys.Count];
            var alreadyLoading = new bool[keys.Count];
            int toLoadCount = 0;
            lock (_loading)
            {
                int i = 0;
                foreach (var k in keys)
                {
                    TaskCompletionSource<Maybe<TValue>> t;
                    alreadyLoading[i] = _loading.TryGetOrAdd(k, NewTcs, out t);
                    if (alreadyLoading[i])
                        toLoadCount++;
                    tcs[i] = t;
                    i++;
                }
            }
            return new BatchLoad(tcs, alreadyLoading, toLoadCount);
        }

        static TaskCompletionSource<Maybe<TValue>> NewTcs(TKey key) => new TaskCompletionSource<Maybe<TValue>>();

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
                Complete(tcs, loaded[j]);
                j++;
            }
        }

        private Maybe<TValue>[] TryGetBatchMixed(IReadOnlyCollection<TKey> keys, BatchLoad batch)
        {
            // construct a list of keys to load
            var keysToLoad = new List<TKey>(batch.ToLoadCount);
            var originalIndexes = new List<int>(batch.ToLoadCount);
            int i = 0;
            foreach (var key in keys)
            {
                if (!batch.AlreadyLoading[i])
                {
                    keysToLoad.Add(key);
                    originalIndexes.Add(i);
                }
                i++;
            }

            // the following line may block
            Maybe<TValue>[] loaded = keysToLoad.Count > 0 ? _dataSource.GetBatch(keysToLoad) : new Maybe<TValue>[0];

            // set the results of the all the TCS we added
            i = 0;
            foreach (var l in loaded)
            {
                int idx = originalIndexes[i];
                Task.Run(() => batch.TaskCompletionSources[idx].TrySetResult(l));
                i++;
            }

            // remove all keys that we loaded
            RemoveAllKeys(keysToLoad);

            // construct results from TCS results
            return ResultsFromBatchTCS(keys, batch);
        }

        static Maybe<TValue>[] ResultsFromBatchTCS(IReadOnlyCollection<TKey> keys, BatchLoad batch)
        {
            var results = new Maybe<TValue>[keys.Count];
            int i = 0;
            foreach (var tcs in batch.TaskCompletionSources)
            {
                results[i] = tcs.Task.Result;
                i++;
            }
            return results;
        }

        public Task<Maybe<TValue>[]> GetBatchAsync(IReadOnlyCollection<TKey> keys) => Task.FromResult(GetBatch(keys)); // TODO: async version

        public void Dispose()
        {
            _dataSource.Invalidated -= _dataSourceInvalidated;
            _dataSource.Evicted -= _dataSourceEvicted;
        }
    }
}