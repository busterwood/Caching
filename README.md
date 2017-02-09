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

| Test | Iterations | Elapsed | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ---------- | ------- | --------------- | ---------- | ------------------ |
| generational cache | 1000 | 0 ms | 324,368 | 37,020 | 37.02 |
| generational cache | 10000 | 5 ms | 3,170,704 | 337,028 | 33.70 |
| generational cache | 100000 | 79 ms | 8,092,948 | 3,017,508 | 30.18 |
| generational cache | 500000 | 329 ms | 24,726,088 | 12,978,060 | 25.96 |
| Bit-Pseudo Lru | 1000 | 0 ms | 332,368 | 39,020 | 39.02 |
| Bit-Pseudo Lru | 10000 | 6 ms | 3,209,644 | 352,732 | 35.27 |
| Bit-Pseudo Lru | 100000 | 87 ms | 6,706,612 | 3,148,456 | 31.48 |
| Bit-Pseudo Lru | 500000 | 323 ms | 26,092,824 | 13,540,636 | 27.08 |
| System.Runtime.Caching | 1000| 20 ms | 557,084 | 361,192 | 361.19 |
| System.Runtime.Caching | 10000| 15 ms | 3,272,256 | 2,207,780 | 220.78 |
| System.Runtime.Caching | 100000| 274 ms | 22,603,416 | 21,616,504 | 216.17 |
| System.Runtime.Caching | 500000| 1,734 ms | 126,253,192 | 113,435,316 | 226.87 |
| Generic Dictionary | 1000| 0 ms | 78,416 | 38,692 | 38.69 |
| Generic Dictionary | 10000| 1 ms | 678,284 | 350,456 | 35.05 |
| Generic Dictionary | 100000| 10 ms | 6,042,836 | 3,128,816 | 31.29 |
| Generic Dictionary | 500000| 67 ms | 25,988,460 | 13,456,616 | 26.91 |
| ConcurrentDictionary | 1000| 0 ms | 133,400 | 1,160,272 | 1,160.27 |
| ConcurrentDictionary | 10000| 1 ms | 908,592 | 403,928 | 40.39 |
| ConcurrentDictionary | 100000| 17 ms | 5,738,268 | 2,994,008 | 29.94 |
| ConcurrentDictionary | 500000| 256 ms | 38,744,308 | 16,608,088 | 33.22 |
