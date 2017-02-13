# Caching
A low memory overhead read-through cache implemented using generations.

The `GenerationalMap<TKey, TValue>` is a read-though cache used for caching items read from an underlying data source.

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
* you can just specify a `halfLife` which will let the cache grow to any size but will ensure items not used for "a long time" are evicted
* you can specify both `gen0Limit` and `halfLife` to combine the attributes of both

# Performance and Memory

The following tests compare a generation cache with a Bit-Pseduo LRU cache.

### 4 threads reading-through a total of 100,000 items
| Test | Elapsed | Items in cache | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ------- | -------------- | --------------- | ---------- | ------------------ |
| BitPseudoLru 50% | 118 ms | 50,000 | 2,973,144 | 1,519,864 | 30.40 |
| Generational 25% Gen0 Limit| 34 ms | 33,332 | 3,782,700 | 895,764 | 26.87 |
| Generational 50ms Half-life | 25 ms | 100,000 | 6,050,996 | 3,129,228 | 31.29 |
| concurrent_dictionary_memory_overhead | 11 ms | 100,000 | 5,651,692 | 2,994,008 | 29.94 |

### 4 threads reading-through a total of 500,000 items
| Test | Elapsed | Items in cache | Bytes allocated | Bytes held | Bytes held per key |
| ---- | ------- | -------------- | --------------- | ---------- | ------------------ |
| BitPseudoLru 50% | 2,062 ms | 250,000 | 9,686,520 | 6,529,452 | 26.12 |
| Generational 25% Gen0 Limit | 183 ms | 166,664 | 11,926,556 | 4,637,388 | 27.82 |
| Generational 50ms Half-life | 159 ms | 331,819 | 31,131,248 | 9,617,896 | 28.99 |
| concurrent_dictionary_memory_overhead | 205 ms | 500,000 | 37,960,620 | 16,607,692 | 33.22 |
