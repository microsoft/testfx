// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures the side-effect-free fast path of <see cref="AssertExtensions.That"/>, which compiles (or
/// interprets) the condition's expression tree once per call.
/// </summary>
[MemoryDiagnoser]
public class AssertThatBenchmarks
{
    private int _a;
    private int _b;

    [GlobalSetup]
    public void Setup()
    {
        _a = 5;
        _b = 5;
    }

    [Benchmark]
    public void That_SideEffectFree() => Assert.That(() => _a == _b);
}
