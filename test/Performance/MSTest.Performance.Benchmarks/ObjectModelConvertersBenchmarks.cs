// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <c>ObjectModelConverters.FixUpTestCase</c>, which runs once per <see cref="TestCase"/> discovered
/// or executed through the VSTest bridge - a hot path for every VSTest-driven test run.
/// </summary>
[MemoryDiagnoser]
public class ObjectModelConvertersBenchmarks
{
    private TestCase _testCase = null!;

    // FixUpTestCase mutates the TestCase (adds a property and replaces the executor URI), so each iteration
    // needs a fresh instance to keep the benchmark representative of the real once-per-test-case call site.
    [IterationSetup]
    public void IterationSetup()
        => _testCase = new TestCase("Namespace.Class.Method", new Uri("executor://SomeOtherAdapter", UriKind.Absolute), "source.cs");

    [Benchmark]
    public void FixUpTestCase()
        => _testCase.FixUpTestCase();
}
