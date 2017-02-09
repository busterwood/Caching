# Caching
A low memory overhead read-through cache implemented using generations.

The `GenerationalMap<TKey, TValue>` is a read-though cache that is typically used for caching items read from an underlying data source.

# How does it work?

The design is insipred by "generational garbage collection" in that:

* the cache as two generations `Gen0` and `Gen1`
* as items are read from the underlying data source they are added to `Gen0`
* when a collection happens `Gen1` is thrown away and `Gen0` is moved to `Gen1`
* when an item is read from `Gen1` it is promted back to `Gen0`

# When does a collection happen?

The `GenerationalMap<TKey, TValue>` contructor takes two arguments that control collection:

* if a `gen0Limit` is set then a collection will occur when `Gen0` reaches that limit
* if `halfLife` is set then a collection will occur after that period of time (unless the `gen0Limit` was reached since the last collection)

One or both parameters neeed to be set, i.e.

* you can just specify a `gen0Limit`, but then an the cache will never be cleared, even if it is not used again for a long time
* you can just specify a `halfLife` and let the cache grow to any size, just collection at regular intervals
* you can specify both `gen0Limit` and `halfLife` to combine the attributes of both

# Benchmarks

## 1,000 items

| Test | Elapsed | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ------- | --------------- | ---------- | ------------------ |
| Generic Dictionary | 0 ms | 78,416 | 38,692 | 38.69 |
| ConcurrentDictionary | 0 ms | 133,400 | 1,160,272 | 1,160.27 |
| generational cache |  0 ms | 324,368 | 37,020 | 37.02 |
| Bit-Pseudo Lru |  0 ms | 332,368 | 39,020 | 39.02 |
| System.Runtime.Caching | 20 ms | 557,084 | 361,192 | 361.19 |


## 100,000 items

| Test | Elapsed | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ------- | --------------- | ---------- | ------------------ |
| Generic Dictionary | 10 ms | 6,042,836 | 3,128,816 | 31.29 |
| ConcurrentDictionary | 17 ms | 5,738,268 | 2,994,008 | 29.94 |
| generational cache |  79 ms | 8,092,948 | 3,017,508 | 30.18 |
| Bit-Pseudo Lru |  87 ms | 6,706,612 | 3,148,456 | 31.48 |
| System.Runtime.Caching | 274 ms | 22,603,416 | 21,616,504 | 216.17 |


## 500,000 items

| Test | Elapsed | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ------- | --------------- | ---------- | ------------------ |
| Generic Dictionary | 67 ms | 25,988,460 | 13,456,616 | 26.91 |
| ConcurrentDictionary | 256 ms | 38,744,308 | 16,608,088 | 33.22 |
| generational cache |  329 ms | 24,726,088 | 12,978,060 | 25.96 |
| Bit-Pseudo Lru |  323 ms | 26,092,824 | 13,540,636 | 27.08 |
| System.Runtime.Caching | 1,734 ms | 126,253,192 | 113,435,316 | 226.87 |
