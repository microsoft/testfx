// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <c>ObjectModelConverters.FixUpTestCase</c>, which the VSTest bridge invokes once per
/// <see cref="TestCase"/> to replace the executor URI with its own. This is a per-test-case hot path
/// exercised for every discovered or executed test when running through the VSTest bridge.
/// </summary>
[MemoryDiagnoser]
public class ObjectModelConvertersBenchmarks
{
    private static readonly Uri ExecutorUri = new("executor://sample", UriKind.Absolute);

    private TestCase _testCase = null!;

    [IterationSetup]
    public void IterationSetup()
        // FixUpTestCase mutates the TestCase (adds a property, changes ExecutorUri), so a fresh
        // instance with a handful of pre-populated properties (approximating a typical discovered
        // test case) is required for each invocation to keep the benchmark representative.
        => _testCase = new TestCase("SomeNamespace.SomeClass.SomeTestMethod", ExecutorUri, "source.cs")
        {
            DisplayName = "SomeTestMethod",
            CodeFilePath = "source.cs",
            LineNumber = 42,
        };

    [Benchmark]
    public void FixUpTestCase()
        => _testCase.FixUpTestCase();
}
