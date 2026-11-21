// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestElement = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestElement;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class AssemblyEnumeratorMutationTests : TestContainer
{
    private const string Source = "discovery-source";

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider = new();

    public AssemblyEnumeratorMutationTests()
    {
        // The real reflection operations are used on purpose: the fixtures below are real types.
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
        _testablePlatformServiceProvider.MockFileOperations.Setup(fileOperations => fileOperations.LoadAssembly(Source)).Returns(typeof(Sample).Assembly);
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            ReflectHelper.ClearCache();
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = null;
        }
    }

#if DEBUG && !NETFRAMEWORK
    public void EnumerateAssemblyAssertsThatTheAssemblyFileNameIsNotBlank()
    {
        const string blankSource = " ";
        _testablePlatformServiceProvider.MockFileOperations.Setup(fileOperations => fileOperations.LoadAssembly(blankSource)).Returns(typeof(Sample).Assembly);
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        AssemblyEnumerationResult result = new AssemblyEnumerator().EnumerateAssembly(blankSource, false, false, [], null);

        result.TestElements.Should().BeEmpty();
        assertions.Should().Equal("Invalid assembly file name.");
    }
#endif

    public void EnumerateAssemblyOnlyEnumeratesTheProvidedTypesInsteadOfAllTheTypesOfTheAssembly()
    {
        var observedTypes = new List<Type>();

        AssemblyEnumerationResult result = new AssemblyEnumerator().EnumerateAssembly(Source, false, false, [typeof(Sample)], (type, _) => observedTypes.Add(type));

        observedTypes.Should().Equal(typeof(Sample), typeof(Sample));
        result.TestElements.Select(test => test.TestMethod.Name).Should().Contain(nameof(Sample.Plain));
    }

    public void EnumerateAssemblyReportsAndTracesTheTypesThatCannotBeInspected()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        string expectedWarning = string.Format(CultureInfo.CurrentCulture, Resource.CouldNotInspectTypeDuringDiscovery, typeof(Sample).FullName, Source, "boom");
        string expectedTrace = "AssemblyEnumerator: " + expectedWarning;

        AssemblyEnumerationResult result = new ThrowingAssemblyEnumerator().EnumerateAssembly(Source, false, false, [typeof(Sample)], null);

        result.TestElements.Should().BeEmpty();
        result.Warnings.Should().Equal(expectedWarning);
        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(expectedTrace), Times.Once);
    }

    public void EnumerateAssemblyStillReportsTheTypesThatCannotBeInspectedWhenInformationTracingIsDisabled()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        string expectedWarning = string.Format(CultureInfo.CurrentCulture, Resource.CouldNotInspectTypeDuringDiscovery, typeof(Sample).FullName, Source, "boom");

        AssemblyEnumerationResult result = new ThrowingAssemblyEnumerator().EnumerateAssembly(Source, false, false, [typeof(Sample)], null);

        result.Warnings.Should().Equal(expectedWarning);
        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>()), Times.Never);
    }

    public void EnumerateAssemblyUsesTheDefaultDisplayNameForRowsWhenTheDataSourceDoesNotProvideOne()
    {
        AssemblyEnumerationResult result = Discover(nameof(Sample.Plain));

        result.TestElements.Select(test => test.TestMethod.DisplayName).Should().Equal("Plain (1,\"a\")", "Plain (2,\"b\")");
    }

    public void EnumerateAssemblyDoesNotCreateTestCategoriesFromAnEmptyListOfRowCategories()
    {
        AssemblyEnumerationResult result = Discover(nameof(Sample.EmptyRowCategories), clearCategories: true);

        UnitTestElement test = result.TestElements.Should().ContainSingle().Subject;
        test.TestMethod.ActualData.Should().Equal(1);
        test.TestCategory.Should().BeNull();
    }

    public void EnumerateAssemblyUsesTheRowCategoriesWhenTheTestHasNoCategories()
    {
        AssemblyEnumerationResult result = Discover(nameof(Sample.RowCategories), clearCategories: true);

        UnitTestElement test = result.TestElements.Should().ContainSingle().Subject;
        test.TestMethod.ActualData.Should().Equal(1);
        test.TestCategory.Should().Equal("row");
    }

    public void EnumerateAssemblyPutsTheRowCategoriesBeforeTheCategoriesOfTheTest()
    {
        AssemblyEnumerationResult result = Discover(nameof(Sample.RowCategories), clearCategories: false);

        UnitTestElement test = result.TestElements.Should().ContainSingle().Subject;
        test.TestMethod.ActualData.Should().Equal(1);
        test.TestCategory.Should().Equal("row", "method");
    }

    private static AssemblyEnumerationResult Discover(string method, bool clearCategories = false)
        => new SampleAssemblyEnumerator(method, clearCategories).EnumerateAssembly(Source, false, false, [typeof(Sample)], null);

    private sealed class ThrowingAssemblyEnumerator : AssemblyEnumerator
    {
        internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
            => new ThrowingTypeEnumerator(type, assemblyFileName);
    }

    private sealed class ThrowingTypeEnumerator(Type type, string source)
        : TypeEnumerator(type, source, ReflectHelper.Instance, new TypeValidator(ReflectHelper.Instance), new TestMethodValidator(ReflectHelper.Instance, false))
    {
        internal override List<UnitTestElement>? Enumerate(List<string> warnings)
            => throw new InvalidOperationException("boom");
    }

    private sealed class SampleAssemblyEnumerator(string method, bool clearCategories) : AssemblyEnumerator
    {
        internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
            => new SampleTypeEnumerator(type, assemblyFileName, discoverInternals, method, clearCategories);
    }

    private sealed class SampleTypeEnumerator(Type type, string source, bool internals, string method, bool clearCategories)
        : TypeEnumerator(type, source, ReflectHelper.Instance, new TypeValidator(ReflectHelper.Instance, internals), new TestMethodValidator(ReflectHelper.Instance, internals))
    {
        internal override List<UnitTestElement>? Enumerate(List<string> warnings)
        {
            var tests = base.Enumerate(warnings)?.Where(test => test.TestMethod.Name == method).ToList();
            if (clearCategories && tests is not null)
            {
                foreach (UnitTestElement test in tests)
                {
                    test.TestCategory = null;
                }
            }

            return tests;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RowsAttribute(string kind) : Attribute, ITestDataSource
    {
        public IEnumerable<object?[]> GetData(MethodInfo methodInfo)
        {
            switch (kind)
            {
                case "plain":
                    yield return [1, "a"];
                    yield return [2, "b"];
                    break;
                case "emptyCategories":
                    yield return [new TestDataRow<int>(1) { TestCategories = [] }];
                    break;
                case "rowCategories":
                    yield return [new TestDataRow<int>(1) { TestCategories = ["row"] }];
                    break;
            }
        }

        public string? GetDisplayName(MethodInfo methodInfo, object?[]? data)
            => null;
    }

    [TestClass]
    public sealed class Sample
    {
        [TestMethod]
        [Rows("plain")]
        public void Plain(int number, string text)
        {
        }

        [TestMethod]
        [Rows("emptyCategories")]
        public void EmptyRowCategories(int number)
        {
        }

        [TestMethod]
        [TestCategory("method")]
        [Rows("rowCategories")]
        public void RowCategories(int number)
        {
        }
    }
}
