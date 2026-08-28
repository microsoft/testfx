// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

[MemoryDiagnoser]
public class MSTestTestNodeConverterBenchmarks
{
    private UnitTestElement _plainElement = null!;
    private UnitTestElement _parameterizedElement = null!;
    private UnitTestElement _metadataElement = null!;

    [GlobalSetup]
    public void Setup()
    {
        _plainElement = CreateElement("TestMethod");
        _parameterizedElement = CreateElement("TestMethod(System.String,System.Int32)");
        _metadataElement = CreateElement("TestMethod");
        _metadataElement.TestCategory = ["Fast", "Unit"];
        _metadataElement.Traits = [new("Owner", "MSTest")];

        // Populate caches before measurement. These benchmarks target steady-state node conversion;
        // cold discovery and process startup are covered by MSTest.Performance.Runner.
        _ = MSTestTestNodeConverter.ToDiscoveredTestNode(_plainElement, isTrxEnabled: false);
        _ = MSTestTestNodeConverter.ToDiscoveredTestNode(_parameterizedElement, isTrxEnabled: false);
        _ = MSTestTestNodeConverter.ToDiscoveredTestNode(_metadataElement, isTrxEnabled: false);
    }

    [Benchmark(Baseline = true)]
    public TestNode ConvertPlainTest()
        => MSTestTestNodeConverter.ToDiscoveredTestNode(_plainElement, isTrxEnabled: false);

    [Benchmark]
    public TestNode ConvertParameterizedTest()
        => MSTestTestNodeConverter.ToDiscoveredTestNode(_parameterizedElement, isTrxEnabled: false);

    [Benchmark]
    public TestNode ConvertTestWithMetadata()
        => MSTestTestNodeConverter.ToDiscoveredTestNode(_metadataElement, isTrxEnabled: false);

    private static UnitTestElement CreateElement(string managedMethodName)
    {
        var testMethod = new TestMethod(
            managedMethodName,
            hierarchyValues: null,
            name: "TestMethod",
            fullClassName: "Benchmarks.TestClass",
            assemblyName: "Benchmarks.dll",
            displayName: null,
            parameterTypes: null);

        return new UnitTestElement(testMethod);
    }
}
