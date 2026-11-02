// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace MSTest.Analyzers.UnitTests.Helpers;

[TestClass]
public sealed class ArrayBuilderTests
{
    [TestMethod]
    public void SetItem_GrowsWithDefaultsAndReplacesExistingSlots()
    {
        using Builder<int> builder = new(2);
        builder.Call("SetItem", [typeof(int), typeof(int)], 3, 42);
        Assert.AreSequenceEqual([0, 0, 0, 42], builder.Items);
        builder.Call("SetItem", [typeof(int), typeof(int)], 1, 9);
        builder.Call("SetItem", [typeof(int), typeof(int)], 4, 11);
        Assert.AreSequenceEqual([0, 9, 0, 42, 11], builder.Items);
        builder.Set(0, 7);
        Assert.AreEqual(7, builder.Items[0]);
    }

    [TestMethod]
    public void EditOperations_PreserveOrderingAndRangeBoundaries()
    {
        using Builder<int> builder = new();
        builder.Add(3, 1, 4);
        builder.Call("Insert", [typeof(int), typeof(int)], 1, 2);
        Assert.AreSequenceEqual([3, 2, 1, 4], builder.Items);
        builder.Call("RemoveAt", [typeof(int)], 2);
        builder.Call("RemoveLast", []);
        Assert.AreSequenceEqual([3, 2], builder.Items);
        builder.Call("ReverseContents", []);
        Assert.AreSequenceEqual([2, 3], builder.Items);
        Assert.AreEqual(2, builder.Call("First", []));
        Assert.AreEqual(3, builder.Call("Last", []));
        Assert.IsTrue((bool)builder.Call("Any", [])!);
        builder.Call("Clear", []);
        Assert.HasCount(0, builder.Items);
        Assert.IsFalse((bool)builder.Call("Any", [])!);
    }

    [TestMethod]
    public void FindAndIndex_SearchOnlyRequestedRangeAndHonorComparer()
    {
        using Builder<string> builder = new();
        builder.Add("zero", "one", "ONE", "last");
        Assert.AreEqual(2, builder.Call("IndexOf", [typeof(string)], "ONE"));
        Assert.AreEqual(1, builder.Call("IndexOf", [typeof(string), typeof(IEqualityComparer<string>)], "ONE", StringComparer.OrdinalIgnoreCase));
        Assert.AreEqual(-1, builder.Call("IndexOf", [typeof(string), typeof(int), typeof(int)], "one", 2, 2));
        Predicate<string> match = value => value == "ONE";
        Assert.AreEqual(2, builder.Call("FindIndex", [typeof(Predicate<string>)], match));
        Assert.AreEqual(2, builder.Call("FindIndex", [typeof(int), typeof(Predicate<string>)], 1, match));
        Assert.AreEqual(-1, builder.Call("FindIndex", [typeof(int), typeof(int), typeof(Predicate<string>)], 0, 2, match));
        Assert.AreEqual(2, builder.Call("FindIndex", [typeof(int), typeof(int), typeof(Predicate<string>)], 2, 1, match));
        Assert.IsTrue((bool)builder.Call("Contains", [typeof(string)], "last")!);
        Assert.IsFalse((bool)builder.Call("Contains", [typeof(string)], "absent")!);
    }

    [TestMethod]
    public void SortOverloads_RespectWholeAndSuffixComparisons()
    {
        using Builder<int> builder = new();
        builder.Add(9, 3, 1, 2);
        builder.Call("Sort", [typeof(int), typeof(IComparer<int>)], 1, Comparer<int>.Default);
        Assert.AreSequenceEqual([9, 1, 2, 3], builder.Items);
        builder.Call("Sort", [typeof(IComparer<int>)], Comparer<int>.Create((left, right) => right.CompareTo(left)));
        Assert.AreSequenceEqual([9, 3, 2, 1], builder.Items);
        builder.Call("Sort", [typeof(Comparison<int>)], (Comparison<int>)((left, right) => left.CompareTo(right)));
        Assert.AreSequenceEqual([1, 2, 3, 9], builder.Items);
        builder.Call("ReverseContents", []);
        builder.Call("Sort", []);
        Assert.AreSequenceEqual([1, 2, 3, 9], builder.Items);
    }

    [TestMethod]
    public void RangeAndInitializationOperations_UseExactCounts()
    {
        using Builder<int> builder = new();
        builder.Call("AddRange", [typeof(int[]), typeof(int), typeof(int)], new[] { 8, 1, 2, 9 }, 1, 2);
        builder.Call("AddRange", [typeof(int[]), typeof(int)], new[] { 3, 4, 5 }, 2);
        builder.Call("AddRange", [typeof(IEnumerable<int>)], new List<int> { 6, 7 });
        builder.Call("AddRange", [typeof(ImmutableArray<int>), typeof(int)], ImmutableArray.Create(10, 11, 12), 2);
        builder.Call("AddRange", [typeof(ImmutableArray<int>)], ImmutableArray.Create(13, 14));
        Assert.AreSequenceEqual([1, 2, 3, 4, 6, 7, 10, 11, 13, 14], builder.Items);
        builder.Call("Clip", [typeof(int)], 3);
        Assert.AreSequenceEqual([1, 2, 3], builder.Items);
        builder.Call("ZeroInit", [typeof(int)], 2);
        Assert.AreSequenceEqual([0, 0], builder.Items);
        builder.Call("AddMany", [typeof(int), typeof(int)], 5, 3);
        builder.Call("AddMany", [typeof(int), typeof(int)], 99, 0);
        Assert.AreSequenceEqual([0, 0, 5, 5, 5], builder.Items);
        int[] destination = [-1, -1, -1, -1, -1, -1, -1];
        builder.Call("CopyTo", [typeof(int[]), typeof(int)], destination, 1);
        Assert.AreSequenceEqual([-1, 0, 0, 5, 5, 5, -1], destination);
    }

    [TestMethod]
    public void Snapshots_DoNotChangeWhenBuilderIsEditedAndDistinguishDefaultEmpty()
    {
        using Builder<int> builder = new(3);
        Assert.IsTrue(((ImmutableArray<int>)builder.Call("ToImmutableOrNull", [])!).IsDefault);
        builder.Add(1, 2, 3);
        var snapshot = (ImmutableArray<int>)builder.Call("ToImmutable", [])!;
        int[] array = (int[])builder.Call("ToArray", [])!;
        builder.Set(1, 42);
        Assert.AreSequenceEqual([1, 2, 3], snapshot);
        Assert.AreSequenceEqual([1, 2, 3], array);
        Assert.AreSequenceEqual([1, 42, 3], builder.Items);
        Assert.AreSequenceEqual([1, 42, 3], (ImmutableArray<int>)builder.Call("ToImmutableOrNull", [])!);
    }

    [TestMethod]
    [DataRow(127, true)]
    [DataRow(128, false)]
    public void Pool_ReturnsClearedSmallBuildersAndDiscardsLargeOnes(int capacity, bool retained)
    {
        Type type = Builder<int>.BuilderType;
        object pool = type.GetMethod("CreatePool", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(int)], null)!.Invoke(null, [2])!;
        MethodInfo allocate = pool.GetType().GetMethod("Allocate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object original = allocate.Invoke(pool, null)!;
        var builder = new Builder<int>(original);
        builder.Call("EnsureCapacity", [typeof(int)], capacity);
        builder.Add(11, 22);
        builder.Dispose();
        object next = allocate.Invoke(pool, null)!;
        using Builder<int> reused = new(next);
        Assert.AreEqual(retained, ReferenceEquals(original, next));
        Assert.HasCount(0, reused.Items);
    }

    [TestMethod]
    public void FreezeAndFree_PooledResultsSurviveReuseForExactAndSpareCapacity()
    {
        foreach (int capacity in new[] { 2, 8 })
        {
            object pool = Builder<int>.BuilderType.GetMethod("CreatePool", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(int)], null)!.Invoke(null, [2])!;
            MethodInfo allocate = pool.GetType().GetMethod("Allocate", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object instance = allocate.Invoke(pool, null)!;
            var builder = new Builder<int>(instance);
            builder.Call("EnsureCapacity", [typeof(int)], capacity);
            // The default constructor capacity is eight; Count eight exercises MoveToImmutable.
            builder.Add(1, 2, 3, 4, 5, 6, 7, 8);
            if (capacity == 8)
            {
                builder.Call("EnsureCapacity", [typeof(int)], 16);
            }

            var frozen = (ImmutableArray<int>)builder.Call("ToImmutableAndFree", [])!;
            using Builder<int> next = new(allocate.Invoke(pool, null)!);
            next.Add(99);
            Assert.AreSequenceEqual([1, 2, 3, 4, 5, 6, 7, 8], frozen);
            Assert.AreSequenceEqual([99], next.Items);
        }
    }

    [TestMethod]
    public void Grouping_DistinguishesEmptySingletonAndCaseInsensitiveBuckets()
    {
        using Builder<string> builder = new();
        Func<string, string> key = value => value.Substring(0, 1);
        MethodInfo method = Builder<string>.BuilderType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == "ToDictionary").MakeGenericMethod(typeof(string));
        var empty = (Dictionary<string, ImmutableArray<string>>)method.Invoke(builder.Instance, [key, StringComparer.OrdinalIgnoreCase])!;
        Assert.HasCount(0, empty);
        builder.Add("apple");
        var single = (Dictionary<string, ImmutableArray<string>>)method.Invoke(builder.Instance, [key, StringComparer.OrdinalIgnoreCase])!;
        Assert.AreSequenceEqual(["apple"], single["A"]);
        builder.Add("Apricot", "banana", "avocado");
        var multiple = (Dictionary<string, ImmutableArray<string>>)method.Invoke(builder.Instance, [key, StringComparer.OrdinalIgnoreCase])!;
        Assert.HasCount(2, multiple);
        Assert.AreSequenceEqual(["apple", "Apricot", "avocado"], multiple["a"]);
        Assert.AreSequenceEqual(["banana"], multiple["B"]);
    }

    [TestMethod]
    public void DistinctOperations_PreserveFirstOccurrenceAndProjectBeforeDeduplicating()
    {
        using Builder<int> builder = new();
        builder.Add(3, 1, 3, 2, 1, 4);
        builder.Call("RemoveDuplicates", []);
        Assert.AreSequenceEqual([3, 1, 2, 4], builder.Items);
        MethodInfo select = Builder<int>.BuilderType.GetMethods().Single(method => method.Name == "SelectDistinct").MakeGenericMethod(typeof(int));
        var result = (ImmutableArray<int>)select.Invoke(builder.Instance, [(Func<int, int>)(value => value % 2)])!;
        Assert.AreSequenceEqual([1, 0], result);
        Assert.AreSequenceEqual([3, 1, 2, 4], builder.Items);
    }

    [TestMethod]
    public void Downcast_UsesActualDerivedElementsAndReturnsNonDefaultEmpty()
    {
        using Builder<object> builder = new();
        MethodInfo method = Builder<object>.BuilderType.GetMethod("ToDowncastedImmutable")!.MakeGenericMethod(typeof(string));
        var empty = (ImmutableArray<string>)method.Invoke(builder.Instance, null)!;
        Assert.IsFalse(empty.IsDefault);
        Assert.IsEmpty(empty);
        builder.Add("first", "second");
        Assert.AreSequenceEqual(["first", "second"], (ImmutableArray<string>)method.Invoke(builder.Instance, null)!);
    }

    [TestMethod]
    public void Enumerators_ResetAndReportAllItemsThroughBothInterfaces()
    {
        using Builder<int> builder = new();
        builder.Add(4, 5, 6);
        using IEnumerator<int> generic = builder.Items.GetEnumerator();
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => _ = generic.Current);
        Assert.IsTrue(generic.MoveNext());
        Assert.AreEqual(4, generic.Current);
        generic.Reset();
        Assert.IsTrue(generic.MoveNext());
        Assert.AreEqual(4, generic.Current);
        Assert.IsTrue(generic.MoveNext());
        Assert.AreEqual(5, generic.Current);
        Assert.IsTrue(generic.MoveNext());
        Assert.AreEqual(6, generic.Current);
        Assert.IsFalse(generic.MoveNext());
        IEnumerator untyped = ((IEnumerable)builder.Instance).GetEnumerator();
        Assert.IsTrue(untyped.MoveNext());
        Assert.AreEqual(4, untyped.Current);
        untyped.Reset();
        Assert.IsTrue(untyped.MoveNext());
        Assert.AreEqual(4, untyped.Current);
    }

    [TestMethod]
    public void BuilderRanges_PreserveDerivedValuesAndImmutableCovariance()
    {
        using Builder<object> destination = new();
        using Builder<object> same = new();
        same.Add("same");
        destination.Call("AddRange", [Builder<object>.BuilderType], same.Instance);
        using Builder<string> derived = new();
        derived.Add("first", "second");
        MethodInfo builderRange = Builder<object>.BuilderType.GetMethods().Single(method =>
            method.Name == "AddRange" && method.IsGenericMethodDefinition && method.GetParameters()[0].ParameterType.Name.StartsWith("ArrayBuilder", StringComparison.Ordinal));
        builderRange.MakeGenericMethod(typeof(string)).Invoke(destination.Instance, [derived.Instance]);
        MethodInfo immutableRange = Builder<object>.BuilderType.GetMethods().Single(method =>
            method.Name == "AddRange" && method.IsGenericMethodDefinition && method.GetParameters()[0].ParameterType.Name.StartsWith("ImmutableArray", StringComparison.Ordinal));
        immutableRange.MakeGenericMethod(typeof(string)).Invoke(destination.Instance, [ImmutableArray.Create("third")]);
        Assert.AreSequenceEqual(new object[] { "same", "first", "second", "third" }, destination.Items);
    }

    [TestMethod]
    public void CountSetter_ResizesUsingDefaultsAndTruncatesWithoutReordering()
    {
        using Builder<int> builder = new();
        builder.Add(4, 5, 6);
        Builder<int>.BuilderType.GetProperty("Count")!.SetValue(builder.Instance, 5);
        Assert.AreSequenceEqual([4, 5, 6, 0, 0], builder.Items);
        Builder<int>.BuilderType.GetProperty("Count")!.SetValue(builder.Instance, 2);
        Assert.AreSequenceEqual([4, 5], builder.Items);
        builder.Call("EnsureCapacity", [typeof(int)], 1);
        Assert.AreSequenceEqual([4, 5], builder.Items);
    }

    [TestMethod]
    public void StaticPoolFactories_FillExactlyAndReturnIndependentFrozenValues()
    {
        Type type = Builder<int>.BuilderType;
        object filled = type.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public, null, [typeof(int), typeof(int)], null)!.Invoke(null, [3, 7])!;
        Builder<int> builder = new(filled);
        Assert.AreSequenceEqual([7, 7, 7], builder.Items);
        int[] snapshot = (int[])builder.Call("ToArrayAndFree", [])!;
        object empty = type.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public, null, [typeof(int)], null)!.Invoke(null, [4])!;
        using Builder<int> next = new(empty);
        Assert.HasCount(0, next.Items);
        next.Add(99);
        Assert.AreSequenceEqual([7, 7, 7], snapshot);
    }

    [TestMethod]
    public void Downcast_RejectsAnIncompatibleRuntimeElement()
    {
        using Builder<object> builder = new();
        builder.Add(17);
        MethodInfo method = Builder<object>.BuilderType.GetMethod("ToDowncastedImmutable")!.MakeGenericMethod(typeof(string));
        TargetInvocationException error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(builder.Instance, null));
        Assert.IsInstanceOfType<InvalidCastException>(error.InnerException);
    }

    [TestMethod]
    public void DebuggerProjection_ContainsTheCurrentElementsNotUnusedCapacity()
    {
        using Builder<int> builder = new(16);
        builder.Add(3, 1, 4);
        Type proxyType = Builder<int>.BuilderType.GetNestedType("DebuggerProxy", BindingFlags.NonPublic)!.MakeGenericType(typeof(int));
        object proxy = Activator.CreateInstance(proxyType, [builder.Instance])!;
        Assert.AreSequenceEqual([3, 1, 4], (int[])proxyType.GetProperty("A")!.GetValue(proxy)!);
    }

    private sealed class Builder<T> : IDisposable
    {
        internal static Type BuilderType { get; } = typeof(RedundantTestMethodAttributeAnalyzer).Assembly
            .GetType("Analyzer.Utilities.PooledObjects.ArrayBuilder`1", throwOnError: true)!.MakeGenericType(typeof(T));

        internal Builder(int capacity = 8) => Instance = Activator.CreateInstance(BuilderType, [capacity])!;

        internal Builder(object instance) => Instance = instance;

        internal object Instance { get; }

        internal IReadOnlyList<T> Items => (IReadOnlyList<T>)Instance;

        internal void Add(params T[] values) => Call("AddRange", [typeof(T[])], [values]);

        internal void Set(int index, T value) => BuilderType.GetProperty("Item")!.SetValue(Instance, value, [index]);

        internal object? Call(string name, Type[] parameters, params object?[] arguments)
        {
            try
            {
                return BuilderType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameters, null)!.Invoke(Instance, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        public void Dispose() => Call("Dispose", []);
    }
}
