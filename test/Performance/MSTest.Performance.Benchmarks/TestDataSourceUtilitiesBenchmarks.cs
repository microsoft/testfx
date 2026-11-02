// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures the per-data-row display-name computation used by <c>[DataRow]</c>/<c>[DynamicData]</c> tests.
/// This runs once per generated test case during discovery and execution, so it is a hot path for
/// projects with large data-driven test suites.
/// </summary>
[MemoryDiagnoser]
public class TestDataSourceUtilitiesBenchmarks
{
    private static readonly MethodInfo SingleParameterMethod = typeof(TestDataSourceUtilitiesBenchmarks).GetMethod(
        nameof(SampleObjectArrayMethod), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo MultiParameterMethod = typeof(TestDataSourceUtilitiesBenchmarks).GetMethod(
        nameof(SampleMixedTypeMethod), BindingFlags.NonPublic | BindingFlags.Static)!;

    private object?[] _mixedTypeArguments = null!;
    private object?[] _objectArrayArgument = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mixedTypeArguments = ["some string", 42, 'c', null, new[] { 1, 2, 3 }];
        _objectArrayArgument = [_mixedTypeArguments];
    }

    [Benchmark(Baseline = true)]
    public string? ComputeDisplayNameForMixedTypeArguments()
        => TestDataSourceUtilities.ComputeDefaultDisplayName(MultiParameterMethod, _mixedTypeArguments);

    [Benchmark]
    public string? ComputeDisplayNameForObjectArrayArgument()
        => TestDataSourceUtilities.ComputeDefaultDisplayName(SingleParameterMethod, _objectArrayArgument);

    private static void SampleMixedTypeMethod(string s, int i, char c, object? o, int[] arr)
    {
    }

    private static void SampleObjectArrayMethod(object[] data)
    {
    }
}
