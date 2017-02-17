using System;
using System.Linq;
using UnitTests;
using NUnit.Framework;

namespace PerformanceTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var tests = new MixedReadWriteOverheadTests();
            foreach (var method in tests.GetType().GetMethods())
            {
                foreach(var attr in method.GetCustomAttributes(false).OfType<TestCaseAttribute>())
                {
                    //tests.Setup();
                    GC.Collect();
                    var count = (int)attr.Arguments[0];
                    Console.WriteLine($"{Environment.NewLine}{method.Name}({count})");
                    method.Invoke(tests, new object[] { count });
                }
            }
        }
    }
}
