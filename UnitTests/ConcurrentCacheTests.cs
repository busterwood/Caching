using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusterWood.Caching;

namespace UnitTests
{
    public class ConcurrentCacheTests
    {
        [Test]
        public void can_do_something()
        {
            var cache = new ConcurrentGeneralationalCache<int, string>(1000, null);
            var result = cache.Get(1);
        }
    }
}
