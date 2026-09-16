// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <see cref="CollectionAssert.AreEquivalent{T}(System.Collections.Generic.IEnumerable{T}?, System.Collections.Generic.IEnumerable{T}?, System.Collections.Generic.IEqualityComparer{T}?, string?)"/>
/// on its success path, which is the hot path exercised by every passing <c>CollectionAssert.AreEquivalent</c>
/// call in a test suite. Internally this counts element occurrences via dictionaries built per invocation
/// (see <c>CollectionAssert.Helpers.cs</c> <c>GetElementCounts</c>/<c>FindMismatchedElement</c>), so this
/// benchmark tracks the cost of that allocation pattern as collection size grows.
/// </summary>
[MemoryDiagnoser]
public class CollectionAssertEquivalenceBenchmarks
{
    private int[] _smallExpected = null!;
    private int[] _smallActual = null!;
    private int[] _largeExpected = null!;
    private int[] _largeActual = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallExpected = [1, 2, 3, 4, 5];
        _smallActual = [5, 4, 3, 2, 1];

        _largeExpected = Enumerable.Range(0, 1000).ToArray();
        int[] largeActual = Enumerable.Range(0, 1000).ToArray();
        Random.Shared.Shuffle(largeActual);
        _largeActual = largeActual;
    }

    [Benchmark(Baseline = true)]
    public void AreEquivalent_SmallCollection() => CollectionAssert.AreEquivalent(_smallExpected, _smallActual);

    [Benchmark]
    public void AreEquivalent_LargeCollection() => CollectionAssert.AreEquivalent(_largeExpected, _largeActual);
}
