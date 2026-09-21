// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <see cref="ObjectModelConverters.FixUpTestCase(TestCase)"/>, which runs once per <c>TestCase</c>
/// on the VSTestBridge adapter execution path to swap in the bridge's own executor URI while preserving the
/// original one. It short-circuits the property scan via <c>GetProperties().Any(...)</c> instead of a fresh
/// LINQ chain (fixed 2026-08-13); this benchmark tracks the remaining per-call cost/allocation of that scan
/// plus the underlying <c>TestCase.SetPropertyValue</c>/<c>ExecutorUri</c> property-store writes.
/// </summary>
[MemoryDiagnoser]
public class ObjectModelConvertersBenchmarks
{
    private TestCase _testCase = null!;

    // FixUpTestCase mutates the TestCase (replaces ExecutorUri, sets OriginalExecutorUriProperty), so a fresh
    // instance is required per invocation to keep every iteration measuring the "property missing" fast path.
    [IterationSetup]
    public void IterationSetup()
        => _testCase = new TestCase("Namespace.Class.SomeTestMethod", new Uri("executor://original"), "source.cs");

    [Benchmark]
    public void FixUpTestCase()
        => _testCase.FixUpTestCase();
}
