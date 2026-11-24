// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures unordered <c>Assert.AreSequenceEqual</c> calls on their success path,
/// tracking the cost of counting element occurrences as collection size grows.
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
    public void AreEquivalent_SmallCollection()
        => Assert.AreSequenceEqual(_smallExpected, _smallActual, EqualityComparer<int>.Default, SequenceOrder.InAnyOrder, string.Empty);

    [Benchmark]
    public void AreEquivalent_LargeCollection()
        => Assert.AreSequenceEqual(_largeExpected, _largeActual, EqualityComparer<int>.Default, SequenceOrder.InAnyOrder, string.Empty);
}
