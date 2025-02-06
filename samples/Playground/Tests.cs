using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject1;

[TestClass]
public class LotsOfTests
{
    const int NumOfTests = 2000;

    private static ConcurrentDictionary<object, ExecutionContext> dict = GetInstancesExecutionContextsByReflection();

    private static void LogInstancesCount()
        => Console.WriteLine($"Now {dict.Count} instances");

    public static IEnumerable<object[]> DuplicateTests() => Enumerable.Range(0, NumOfTests).Select(x => new object[] { x });

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        System.Diagnostics.Debugger.Break();
        Console.WriteLine("AssemblyInitialize");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Console.WriteLine("AssemblyCleanup");
        LogInstancesCount();
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Console.WriteLine("Hello");
    }

    [TestCleanup]
    public void TestCleanUp()
    {
        Console.WriteLine("Goodbye");
    }

    [TestMethod]
    [TestCategory("Category")]
    [DynamicData(nameof(DuplicateTests), DynamicDataSourceType.Method)]
    public void TestX(int i)
    {
        Allocate(10000);
        Assert.IsTrue(new List<int>().Count >= -i);
        LogInstancesCount();
    }

    // simulates memory allocations per test
    protected readonly string[] memoryAllocations;

    public LotsOfTests()
    {
        this.memoryAllocations = Allocate(200000);
    }

    private static string[] Allocate(int count)
        => Enumerable.Range(0, count).Select(GetString).ToArray();

    private static string GetString(int i)
    {
        var x = 65 + (i % 26);
        return new((Char)x, 8);
    }

    private static ConcurrentDictionary<object, ExecutionContext> GetInstancesExecutionContextsByReflection()
    {
        var assembly = typeof(Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext).Assembly;
        var types = assembly.GetTypes();
        var executionContextService = types.Where(t => t.Name == "ExecutionContextService").First();
        var dictField = executionContextService.GetField("InstancesExecutionContexts", BindingFlags.NonPublic | BindingFlags.Static);
        return (ConcurrentDictionary<object, ExecutionContext>)dictField.GetValue(null) ?? throw new InvalidOperationException();
    }
}
