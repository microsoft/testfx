// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Discovery;

public sealed class AssemblyDiscoveryContinuationTests : TestContainer
{
    public void RowsPreserveMetadataAndDoNotLeakIgnoreReasons()
    {
        using var harness = new DiscoveryHarness();
        UnitTestElement[] rows = harness.Discover(nameof(DataFixture.Rows)).TestElements.ToArray();

        rows.Should().HaveCount(2);
        rows[0].TestMethod.ActualData.Should().Equal(17);
        rows[1].TestMethod.ActualData.Should().Equal(23);
        rows.Select(row => row.TestMethod.TestCaseIndex).Should().Equal(0, 1);
        rows.Select(row => row.TestMethod.DisplayName).Should().Equal("row seventeen", "Rows (twenty-three)");
        rows.Select(row => row.TestMethod.TestDataSourceIgnoreMessage).Should().Equal("row ignored", "source ignored");
        rows[0].TestCategory.Should().Equal("row", "method");
        rows[1].TestCategory.Should().Equal("next", "method");
        rows.Select(row => row.TestMethod.DataType).Should().Equal(DynamicDataType.ITestDataSource, DynamicDataType.ITestDataSource);
    }

    public void TupleAndArrayRowsUseTheirActualParameterContracts()
    {
        using var harness = new DiscoveryHarness();
        UnitTestElement tuple = harness.Discover(nameof(DataFixture.Tuple)).TestElements.Single();
        UnitTestElement array = harness.Discover(nameof(DataFixture.Array)).TestElements.Single();

        tuple.TestMethod.ActualData.Should().Equal(7, "seven");
        tuple.TestMethod.DisplayName.Should().Be("tuple row");
        array.TestMethod.ActualData.Should().Equal(17, "array");
        array.TestMethod.DisplayName.Should().Be("array row");
        tuple.TestMethod.TestCaseIndex.Should().Be(0);
        array.TestMethod.TestCaseIndex.Should().Be(0);
    }

    public void EmptySourcePolicyAndFoldingProduceDistinctObservableRows()
    {
        using var harness = new DiscoveryHarness(TestDataSourceUnfoldingStrategy.Fold);
        UnitTestElement folded = harness.Discover(nameof(DataFixture.Rows)).TestElements.Single();
        folded.TestMethod.DataType.Should().Be(DynamicDataType.None);
        folded.TestMethod.ActualData.Should().BeNull();
        folded.TestMethod.DisplayName.Should().Be("Rows");
        harness.Discover(nameof(DataFixture.ExplicitUnfold)).TestElements.Should().HaveCount(2);

        harness.ConfigureStrategy(TestDataSourceUnfoldingStrategy.Unfold);
        harness.Discover(nameof(DataFixture.ExplicitFold)).TestElements.Should().ContainSingle()
            .Which.TestMethod.DataType.Should().Be(DynamicDataType.None);
        MSTestSettings.PopulateSettings(
            "<RunSettings><MSTest><ConsiderEmptyDataSourceAsInconclusive>true</ConsiderEmptyDataSourceAsInconclusive></MSTest></RunSettings>", null, null);
        UnitTestElement empty = harness.Discover(nameof(DataFixture.Empty)).TestElements.Single();
        empty.TestMethod.DisplayName.Should().Be("empty source label");
        empty.TestMethod.TestDataSourceIgnoreMessage.Should().Be("source ignored");
        empty.TestMethod.DataType.Should().Be(DynamicDataType.None);

        MSTestSettings.PopulateSettings(
            "<RunSettings><MSTest><ConsiderEmptyDataSourceAsInconclusive>false</ConsiderEmptyDataSourceAsInconclusive></MSTest></RunSettings>", null, null);
        UnitTestElement fallback = harness.Discover(nameof(DataFixture.Empty)).TestElements.Single();
        fallback.TestMethod.DisplayName.Should().Be("Empty");
        fallback.TestMethod.TestDataSourceIgnoreMessage.Should().BeNull();
    }

    public void SerializationFailureFallsBackAtomicallyWhileNativeRowsRemainExpanded()
    {
        using var harness = new DiscoveryHarness();
        List<string> warnings = [];
        harness.Services.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);
        harness.Services.MockTraceLogger.Setup(value => value.Warning(It.IsAny<string>())).Callback<string>(warnings.Add);
        UnitTestElement fallback = harness.Discover(nameof(DataFixture.Serialization), mustSerialize: true).TestElements.Single();
        fallback.TestMethod.DataType.Should().Be(DynamicDataType.None);
        fallback.TestMethod.DisplayName.Should().Be("Serialization");
        warnings.Should().ContainSingle();
        warnings[0].Should().Contain("fault row").And.Contain("payload cannot be serialized");

        warnings.Clear();
        UnitTestElement[] native = harness.Discover(nameof(DataFixture.Serialization), mustSerialize: false).TestElements.ToArray();
        native.Should().HaveCount(2);
        native[0].TestMethod.ActualData.Should().Equal(17);
        FaultPayload payload = native[1].TestMethod.ActualData![0].Should().BeOfType<FaultPayload>().Subject;
        payload.SerializationReads.Should().Be(0);
        native.Select(row => row.TestMethod.TestCaseIndex).Should().Equal(0, 1);
        warnings.Should().BeEmpty();
    }

    public void PartialDataFailureRetainsPriorSourcesAndOriginalFallback()
    {
        using var harness = new DiscoveryHarness();
        harness.Services.MockTraceLogger.SetupGet(value => value.IsInfoEnabled).Returns(true);
        List<string> diagnostics = [];
        harness.Services.MockTraceLogger.Setup(value => value.Info(It.IsAny<string>())).Callback<string>(diagnostics.Add);

        UnitTestElement[] rows = harness.Discover(nameof(DataFixture.Partial)).TestElements.ToArray();

        rows.Should().HaveCount(2);
        rows[0].TestMethod.ActualData.Should().Equal(7);
        rows[0].TestMethod.DisplayName.Should().Be("first row");
        rows[0].TestMethod.DataType.Should().Be(DynamicDataType.ITestDataSource);
        rows[1].TestMethod.ActualData.Should().BeNull();
        rows[1].TestMethod.DisplayName.Should().Be("Partial");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Should().Contain("row stream failed");
    }

    public void LoaderFailuresPreserveExactCausesAndDiagnosticOrder()
    {
        using var harness = new DiscoveryHarness();
        harness.Services.SetupMockReflectionOperations();
        Assembly assembly = typeof(DataFixture).Assembly;
        var first = new TypeLoadException("first dependency");
        var second = new FileLoadException("second dependency");
        harness.Services.MockReflectionOperations.Setup(value => value.GetDefinedTypes(assembly))
            .Throws(new ReflectionTypeLoadException([typeof(object)], [first]));
        Action load = () => AssemblyEnumerator.GetTypes(assembly);
        load.Should().ThrowExactly<TypeLoadException>().Which.Should().BeSameAs(first);
        var aggregate = new ReflectionTypeLoadException([typeof(object), typeof(string)], [first, second]);
        harness.Services.MockReflectionOperations.Setup(value => value.GetDefinedTypes(assembly)).Throws(aggregate);
        harness.Services.MockTraceLogger.SetupGet(value => value.IsErrorEnabled).Returns(true);
        List<object?> errors = [];
        harness.Services.MockTraceLogger.Setup(value => value.Error("{0}", It.IsAny<object?[]>()))
            .Callback<string, object?[]>((_, arguments) => errors.Add(arguments[0]));

        load.Should().ThrowExactly<ReflectionTypeLoadException>().Which.Should().BeSameAs(aggregate);
        errors.Should().Equal(first, second);
    }

    public void ObserverSeesBeforeAndAfterTypeDiscovery()
    {
        using var harness = new DiscoveryHarness();
        List<string> observations = [];
        AssemblyEnumerationResult result = harness.Discover(nameof(DataFixture.Rows), observer: (type, tests) =>
        {
            type.Should().Be(typeof(DataFixture));
            observations.Add(tests is null ? "before" : "after:" + tests.Count.ToString(CultureInfo.InvariantCulture));
        });

        result.TestElements.Should().HaveCount(2);
        observations.Should().Equal("before", "after:2");
    }

    private sealed class DiscoveryHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal DiscoveryHarness(TestDataSourceUnfoldingStrategy strategy = TestDataSourceUnfoldingStrategy.Unfold)
        {
            _previous = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = Services;
            Services.MockFileOperations.Setup(value => value.LoadAssembly("discovery-source")).Returns(typeof(DataFixture).Assembly);
            ConfigureStrategy(strategy);
        }

        internal TestablePlatformServiceProvider Services { get; }

        internal void ConfigureStrategy(TestDataSourceUnfoldingStrategy strategy)
            => Services.SetReflectionOperations(new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
            {
                Assembly = typeof(DataFixture).Assembly,
                AssemblyName = typeof(DataFixture).Assembly.GetName().Name!,
                Types = [typeof(DataFixture)],
                AssemblyAttributes = [new TestDataSourceOptionsAttribute(strategy)],
            }));

        internal AssemblyEnumerationResult Discover(string method, bool mustSerialize = false, Action<Type, IReadOnlyList<UnitTestElement>?>? observer = null)
            => new SelectedAssemblyEnumerator(method).EnumerateAssembly("discovery-source", mustSerialize, false, [typeof(DataFixture)], observer);

        public void Dispose()
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = _previous;
        }
    }

    private sealed class SelectedAssemblyEnumerator(string method) : AssemblyEnumerator
    {
        internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
            => new SelectedTypeEnumerator(type, assemblyFileName, discoverInternals, method);
    }

    private sealed class SelectedTypeEnumerator(Type type, string source, bool internals, string method)
        : TypeEnumerator(type, source, ReflectHelper.Instance, new TypeValidator(ReflectHelper.Instance, internals), new TestMethodValidator(ReflectHelper.Instance, internals))
    {
        internal override List<UnitTestElement>? Enumerate(List<string> warnings)
            => base.Enumerate(warnings)?.Where(test => test.TestMethod.Name == method).ToList();
    }

    [TestClass]
    public sealed class DataFixture
    {
        [TestMethod]
        [TestCategory("method")]
        [CoverageRows("rows")]
        public void Rows(int value)
        {
        }

        [TestMethod]
        [CoverageRows("tuple")]
        public void Tuple(int value, string text)
        {
        }

        [TestMethod]
        [CoverageRows("array")]
        public void Array(object?[] values)
        {
        }

        [TestMethod]
        [CoverageRows("empty")]
        public void Empty(int value)
        {
        }

        [TestMethod]
        [CoverageRows("serialization")]
        public void Serialization(object value)
        {
        }

        [TestMethod]
        [CoverageRows("first")]
        [CoverageRows("partial")]
        public void Partial(int value)
        {
        }

        [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
        [CoverageRows("rows")]
        public void ExplicitFold(int value)
        {
        }

        [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)]
        [CoverageRows("rows")]
        public void ExplicitUnfold(int value)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    private sealed class CoverageRowsAttribute(string kind) : Attribute, ITestDataSource, ITestDataSourceIgnoreCapability
    {
        public string? IgnoreMessage { get; set; } = "source ignored";

        public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
        {
            switch (kind)
            {
                case "rows":
                    yield return [new TestDataRow<int>(17) { DisplayName = "row seventeen", IgnoreMessage = "row ignored", TestCategories = ["row"] }];
                    yield return [new TestDataRow<int>(23) { ArgumentsDisplayName = "twenty-three", TestCategories = ["next"] }];
                    break;
                case "tuple":
                    yield return [(7, "seven")];
                    break;
                case "array":
                    yield return [17, "array"];
                    break;
                case "serialization":
                    yield return [17];
                    yield return [new FaultPayload()];
                    break;
                case "first":
                    yield return [7];
                    break;
                case "partial":
                    yield return [9];
                    throw new InvalidOperationException("row stream failed");
            }
        }

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
            => kind switch
            {
                "tuple" => "tuple row",
                "array" => "array row",
                "empty" => "empty source label",
                "first" => "first row",
                "serialization" => data![0] is int ? "valid row" : "fault row",
                _ => null,
            };
    }

    [DataContract]
    private sealed class FaultPayload
    {
        internal int SerializationReads { get; private set; }

        [DataMember]
        public string Value
        {
            get
            {
                SerializationReads++;
                throw new SerializationException("payload cannot be serialized");
            }

            set
            {
                _ = value;
            }
        }
    }
}
