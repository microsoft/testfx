// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

extern alias FrameworkV1;
extern alias FrameworkV2;
extern alias FrameworkV2CoreExtension;

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Moq;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
using TestMethodV1 = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

[TestClass]
public class UnitTestDiscovererTests
{
    private const string Source = "DummyAssembly.dll";
    private UnitTestDiscoverer _unitTestDiscoverer;

    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private Mock<IMessageLogger> _mockMessageLogger;
    private Mock<ITestCaseDiscoverySink> _mockTestCaseDiscoverySink;
    private Mock<IRunSettings> _mockRunSettings;
    private Mock<IDiscoveryContext> _mockDiscoveryContext;
    private UnitTestElement _test;
    private List<UnitTestElement> _testElements;

    [TestInitialize]
    public void TestInit()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _unitTestDiscoverer = new UnitTestDiscoverer();

        _mockMessageLogger = new Mock<IMessageLogger>();
        _mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
        _mockRunSettings = new Mock<IRunSettings>();

        _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);

        _test = new UnitTestElement(new TestMethod("M", "C", "A", false));
        _testElements = new List<UnitTestElement> { _test };
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup()
    {
        _test = null;
        _testElements = null;
        PlatformServiceProvider.Instance = null;
        MSTestSettings.Reset();
    }

    [TestMethodV1]
    public void DiscoverTestsShouldDiscoverForAllSources()
    {
        var sources = new string[] { "DummyAssembly1.dll", "DummyAssembly2.dll" };

        // Setup mocks.
        foreach (var source in sources)
        {
            _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                .Returns(source);
            _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                .Returns(false);
        }

        _unitTestDiscoverer.DiscoverTests(sources, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Assert.
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(2));
    }

    [TestMethodV1]
    public void DiscoverTestsInSourceShouldSendBackAllWarnings()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(false);

        _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Assert.
        _mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Once);
    }

    [TestMethodV1]
    public void DiscoverTestsInSourceShouldSendBackTestCasesDiscovered()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
            .Returns(Source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
            .Returns(true);
        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(Source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        _unitTestDiscoverer.DiscoverTestsInSource(Source, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
    }

    [TestMethodV1]
    public void SendTestCasesShouldNotSendAnyTestCasesIfThereAreNoTestElements()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        // There is a null check for testElements in the code flow before this function call. So not adding a unit test for that.
        _unitTestDiscoverer.SendTestCases(Source, new List<UnitTestElement> { }, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
    }

    [TestMethodV1]
    public void SendTestCasesShouldSendAllTestCaseData()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        _unitTestDiscoverer.SendTestCases(Source, testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    [TestMethodV1]
    public void SendTestCasesShouldSendTestCasesWithoutNavigationDataWhenCollectSourceInformationIsFalse()
    {
        string settingsXml =
        @"<?xml version=""1.0"" encoding=""utf-8""?>
                <RunSettings>
                     <RunConfiguration>
                       <ResultsDirectory>.\TestResults</ResultsDirectory>
                       <CollectSourceInformation>false</CollectSourceInformation>
                     </RunConfiguration>
                </RunSettings>";

        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        SetupNavigation(Source, _test, _test.TestMethod.FullClassName, _test.TestMethod.Name);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(settingsXml);

        // Act
        MSTestSettings.PopulateSettings(_mockDiscoveryContext.Object);
        _unitTestDiscoverer.SendTestCases(Source, _testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.LineNumber == -1)), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.CodeFilePath == null)), Times.Once);
    }

    [TestMethodV1]
    public void SendTestCasesShouldSendTestCasesWithNavigationData()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        SetupNavigation(Source, _test, _test.TestMethod.FullClassName, _test.TestMethod.Name);

        // Act
        _unitTestDiscoverer.SendTestCases(Source, _testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert
        VerifyNavigationDataIsPresent();
    }

    [TestMethodV1]
    public void SendTestCasesShouldSendTestCasesWithNavigationDataWhenDeclaredClassFullNameIsNonNull()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        _test.TestMethod.DeclaringClassFullName = "DC";

        SetupNavigation(Source, _test, _test.TestMethod.DeclaringClassFullName, _test.TestMethod.Name);

        // Act
        _unitTestDiscoverer.SendTestCases(Source, _testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert
        VerifyNavigationDataIsPresent();
    }

    [TestMethodV1]
    public void SendTestCasesShouldUseNaigationSessionForDeclaredAssemblyName()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        _test.TestMethod.DeclaringAssemblyName = "DummyAssembly2.dll";
        ////var test = new UnitTestElement(
        ////    new TestMethod("M", "C", "A", false)
        ////    {
        ////        DeclaringAssemblyName = "DummyAssembly2.dll"
        ////    });

        SetupNavigation(Source, _test, _test.TestMethod.DeclaringClassFullName, _test.TestMethod.Name);

        // Act
        _unitTestDiscoverer.SendTestCases(Source, _testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert
        _testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.CreateNavigationSession("DummyAssembly2.dll"), Times.Once);
    }

    [TestMethodV1]
    public void SendTestCasesShouldSendTestCasesWithNavigationDataForAsyncMethods()
    {
        // Setup mocks.
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
            .Returns((object)null);

        _test.AsyncTypeName = "ATN";

        SetupNavigation(Source, _test, _test.AsyncTypeName, "MoveNext");

        // Act
        _unitTestDiscoverer.SendTestCases(Source, _testElements, _mockTestCaseDiscoverySink.Object, _mockDiscoveryContext.Object, _mockMessageLogger.Object);

        // Assert
        VerifyNavigationDataIsPresent();
    }

    /// <summary>
    /// Send test cases should send filtered test cases only.
    /// </summary>
    [TestMethodV1]
    public void SendTestCasesShouldSendFilteredTestCasesIfValidFilterExpression()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => new TestableTestCaseFilterExpression((p) => (p.DisplayName == "M1")));

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(Source, testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
    }

    /// <summary>
    /// Send test cases should send all test cases if filter expression is null.
    /// </summary>
    [TestMethodV1]
    public void SendTestCasesShouldSendAllTestCasesIfNullFilterExpression()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => null);

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(Source, testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    /// <summary>
    /// Send test cases should send all test cases if GetTestCaseFilter method is not present in DiscoveryContext.
    /// </summary>
    [TestMethodV1]
    public void SendTestCasesShouldSendAllTestCasesIfGetTestCaseFilterNotPresent()
    {
        TestableDiscoveryContextWithoutGetTestCaseFilter discoveryContext = new();

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(Source, testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
    }

    /// <summary>
    /// Send test cases should not send any test cases if filter parsing error.
    /// </summary>
    [TestMethodV1]
    public void SendTestCasesShouldNotSendAnyTestCasesIfFilterError()
    {
        TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new(() => { throw new TestPlatformFormatException("DummyException"); });

        var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
        var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
        var testElements = new List<UnitTestElement> { test1, test2 };

        // Action
        _unitTestDiscoverer.SendTestCases(Source, testElements, _mockTestCaseDiscoverySink.Object, discoveryContext, _mockMessageLogger.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Never);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
    }

    private void SetupNavigation(string source, UnitTestElement test, string className, string methodName)
    {
        var testNavigationData = new DummyNavigationData("DummyFileName.cs", 1, 10);

        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
            .Returns(testNavigationData);
        int minLineNumber = testNavigationData.MinLineNumber;
        string fileName = testNavigationData.FileName;

        _testablePlatformServiceProvider.MockFileOperations.Setup(
            fo =>
            fo.GetNavigationData(
                testNavigationData,
                It.IsAny<string>(),
                It.IsAny<string>(),
                out minLineNumber,
                out fileName));

        var testCase1 = test.ToTestCase();

        SetTestCaseNavigationData(testCase1, testNavigationData.FileName, testNavigationData.MinLineNumber);
    }

    private void SetTestCaseNavigationData(TestCase testCase, string fileName, int lineNumber)
    {
        testCase.LineNumber = lineNumber;
        testCase.CodeFilePath = fileName;
    }

    private void VerifyNavigationDataIsPresent()
    {
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.LineNumber == 1)), Times.Once);
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.CodeFilePath.Equals("DummyFileName.cs"))), Times.Once);
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

internal class TestableUnitTestDiscoverer : UnitTestDiscoverer
{
    internal override void DiscoverTestsInSource(
        string source,
        IMessageLogger logger,
        ITestCaseDiscoverySink discoverySink,
        IDiscoveryContext discoveryContext)
    {
        var testCase1 = new TestCase("A", new System.Uri("executor://testExecutor"), source);
        var testCase2 = new TestCase("B", new System.Uri("executor://testExecutor"), source);
        discoverySink.SendTestCase(testCase1);
        discoverySink.SendTestCase(testCase2);
    }
}

internal class TestableDiscoveryContextWithGetTestCaseFilter : IDiscoveryContext
{
    private readonly Func<ITestCaseFilterExpression> _getFilter;

    public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression> getFilter)
    {
        _getFilter = getFilter;
    }

    public IRunSettings RunSettings { get; }

    public ITestCaseFilterExpression GetTestCaseFilter(
        IEnumerable<string> supportedProperties,
        Func<string, TestProperty> propertyProvider)
    {
        return _getFilter();
    }
}

internal class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
{
    public IRunSettings RunSettings { get; }
}

internal class TestableTestCaseFilterExpression : ITestCaseFilterExpression
{
    private readonly Func<TestCase, bool> _matchTest;

    public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase)
    {
        _matchTest = matchTestCase;
    }

    public string TestCaseFilterValue { get; }

    public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
    {
        return _matchTest(testCase);
    }
}
