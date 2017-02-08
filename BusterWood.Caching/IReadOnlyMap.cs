using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusterWood.Caching
{
    /// <summary>A key to single value cache interface</summary>
    public interface IReadOnlyMap<TKey, TValue>
    {
        TValue Get(TKey key);
        Task<TValue> GetAsync(TKey key);
    }

    /// <summary>A key to many value cache interface</summary>
    public interface IReadOnlyLookup<TKey, TValue>
    {
        IReadOnlyCollection<TValue> Get(TKey key);
        Task<IReadOnlyCollection<TValue>> GetAsync(TKey key);
    }
}
