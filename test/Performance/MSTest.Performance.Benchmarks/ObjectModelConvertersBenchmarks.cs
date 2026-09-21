// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <c>ObjectModelConverters.FixUpTestCase</c>, which VSTestBridge calls once per discovered or
/// executed <see cref="TestCase"/> to replace the framework's executor URI with the platform's own.
/// </summary>
[MemoryDiagnoser]
[InvocationCount(1)]
public class ObjectModelConvertersBenchmarks
{
    private static readonly Uri OriginalExecutorUri = new("executor://sample.testadapter", UriKind.Absolute);

    private TestCase _testCase = null!;

    [IterationSetup]
    public void IterationSetup()
        => _testCase = new TestCase("SomeNamespace.SomeClass.SomeTestMethod", OriginalExecutorUri, "source.cs");

    [Benchmark]
    public void FixUpTestCase()
        => _testCase.FixUpTestCase();
}
