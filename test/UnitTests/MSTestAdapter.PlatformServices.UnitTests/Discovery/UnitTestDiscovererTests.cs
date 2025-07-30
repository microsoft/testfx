// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public class UnitTestDiscovererTests : TestContainer
{
    private const string Source = "DummyAssembly.dll";
    private readonly UnitTestDiscoverer _unitTestDiscoverer;

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private readonly Mock<IMessageLogger> _mockMessageLogger;
    private readonly Mock<ITestCaseDiscoverySink> _mockTestCaseDiscoverySink;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<IDiscoveryContext> _mockDiscoveryContext;
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler;
    private readonly UnitTestElement _test;
    private readonly List<UnitTestElement> _testElements;

    public UnitTestDiscovererTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _mockTestSourceHandler = new();
        _unitTestDiscoverer = new UnitTestDiscoverer(_mockTestSourceHandler.Object);

        _mockMessageLogger = new Mock<IMessageLogger>();
        _mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
        _mockRunSettings = new Mock<IRunSettings>();

        _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);

        _test = new UnitTestElement(new TestMethod("M", "C", "A", false));
        _testElements = [_test];
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            _testElements.Clear();
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public void DiscoverTestsShouldThrowOnFileNotFound()
    {
        string[] sources = ["DummyAssembly1.dll", "DummyAssembly2.dll"];

        // Setup mocks.
        foreach (string source in sources)
        {
            _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                .Returns(source);
            _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                .Returns(false);
        }

        Exception ex = VerifyThrows<FileNotFoundException>(() => _unitTestDiscoverer.DiscoverTests(sources, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object));
        Verify(ex.Message == string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, sources[0]));
    }

    public void DiscoverTestsInSourceShouldThrowOnFileNotFound()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(false);

        Exception ex = VerifyThrows<FileNotFoundException>(() => _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object));
        Verify(ex.Message == string.Format(CultureInfo.CurrentCulture, Resource.TestAssembly_FileDoesNotExist, Source));
    }

    public void DiscoverTestsInSourceShouldSendBackTestCasesDiscovered()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(true);
        _mockTestSourceHandler.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(Source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        _mockRunSettings.Setup(rs => rs.SettingsXml)
            .Returns("""
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <MSTestV2>
                <TreatDiscoveryWarningsAsErrors>False</TreatDiscoveryWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        // Act
        _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
    }

    public void DiscoverTestsInSourceShouldThrowWhenTreatDiscoveryWarningsAsErrorsIsTrue()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(true);
        _mockTestSourceHandler.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(Source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        string settingsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <MSTestV2>
                <TreatDiscoveryWarningsAsErrors>True</TreatDiscoveryWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """;

        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(settingsXml);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        // Act
        Action action = () => _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Assert
        action.Should().Throw<MSTestException>()
            .WithMessage($"*{Source}*");

        // Verify warning message was sent to logger (not error)
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Never);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Never);
    }

    public void DiscoverTestsInSourceShouldNotThrowWhenTreatDiscoveryWarningsAsErrorsIsFalse()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(true);
        _mockTestSourceHandler.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(Source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        _mockRunSettings.Setup(rs => rs.SettingsXml)
            .Returns("""
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <MSTestV2>
                <TreatDiscoveryWarningsAsErrors>False</TreatDiscoveryWarningsAsErrors>
              </MSTestV2>
            </RunSettings>
            """);
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object, _mockMessageLogger.Object, null);

        // Act & Assert
        // Should not throw an exception
        _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Verify warning message was sent to logger (not error)
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.AtLeastOnce);
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Error, It.IsAny<string>()), Times.Never);
    }

    public void SendTestCasesShouldNotSendAnyTestCasesIfThereAreNoTestElements()
    {
        // There is a null check for testElements in the code flow before this function call. So not adding a unit test for that.
        _unitTestDiscoverer.SendTestCases(new List<UnitTestElement> { }, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
    }

    public void SendTestCasesShouldSendAllTestCaseData()
    {
        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        _unitTestDiscoverer.SendTestCases(testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    /// <summary>
    /// Send test cases should send filtered test cases only.
    /// </summary>
    public void SendTestCasesShouldSendFilteredTestCasesIfValidFilterExpression()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => new TestableTestCaseFilterExpression(p => p.DisplayName == "M1"));

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
    }

    /// <summary>
    /// Send test cases should send all test cases if filter expression is null.
    /// </summary>
    public void SendTestCasesShouldSendAllTestCasesIfNullFilterExpression()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => null!);

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    /// <summary>
    /// Send test cases should send all test cases if GetTestCaseFilter method is not present in DiscoveryContext.
    /// </summary>
    public void SendTestCasesShouldSendAllTestCasesIfGetTestCaseFilterNotPresent()
    {
        TestableDiscoveryContextWithoutGetTestCaseFilter discoveryContext = new();

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    /// <summary>
    /// Send test cases should not send any test cases if filter parsing error.
    /// </summary>
    public void SendTestCasesShouldNotSendAnyTestCasesIfFilterError()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => throw new TestPlatformFormatException("DummyException"));

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Never);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
    }
}

internal class DummyNavigationData
{
    public DummyNavigationData(string fileName, int minLineNumber, int maxLineNumber)
    {
        FileName = fileName;
        MinLineNumber = minLineNumber;
        MaxLineNumber = maxLineNumber;
    }

    public string FileName { get; set; }

    public int MinLineNumber { get; set; }

    public int MaxLineNumber { get; set; }
}

internal class TestableUnitTestDiscoverer(ITestSourceHandler? testSourceHandler = null) : UnitTestDiscoverer(testSourceHandler ?? new TestSourceHandler())
{
    internal override void DiscoverTestsInSource(
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink,
        IDiscoveryContext? discoveryContext)
    {
        var testCase1 = new TestCase("A", new Uri("executor://testExecutor"), source);
        var testCase2 = new TestCase("B", new Uri("executor://testExecutor"), source);
        discoverySink.SendTestCase(testCase1);
        discoverySink.SendTestCase(testCase2);
    }
}

internal class TestableDiscoveryContextWithGetTestCaseFilter : IDiscoveryContext
{
    private readonly Func<ITestCaseFilterExpression?> _getFilter;

    public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression?> getFilter) => _getFilter = getFilter;

    public IRunSettings? RunSettings { get; }

    public ITestCaseFilterExpression? GetTestCaseFilter(
        IEnumerable<string>? supportedProperties,
        Func<string, TestProperty?> propertyProvider) => _getFilter();
}

internal sealed class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
{
    public IRunSettings? RunSettings { get; }
}

internal sealed class TestableTestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly Func<TestCase, bool> _matchTest;

    public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase) => _matchTest = matchTestCase;

    public string TestCaseFilterValue => null!;

    public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider) => _matchTest(testCase);
}
