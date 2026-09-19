// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <c>ObjectModelConverters.FixUpTestCase</c> (VSTestBridge), which runs once per discovered/executed
/// <see cref="TestCase"/> to replace the test framework's executor URI with the bridge's own, so it is a hot path
/// for any VSTest-based run.
/// </summary>
[MemoryDiagnoser]
public class ObjectModelConvertersBenchmarks
{
    private static readonly Uri OriginalExecutorUri = new("executor://SomeTestFramework/v1");

    // FixUpTestCase mutates the TestCase (records the original executor URI, replaces ExecutorUri), so a fresh
    // instance is required for every invocation to keep the benchmarked work representative of the real per-test
    // cost rather than measuring the already-fixed-up fast path on later iterations.
    private TestCase _testCase = null!;

    [IterationSetup]
    public void IterationSetup() => _testCase = new TestCase("SomeNamespace.SomeClass.SomeTest", OriginalExecutorUri, "SomeSource");

    [Benchmark]
    public void FixUpTestCase() => _testCase.FixUpTestCase();
}
