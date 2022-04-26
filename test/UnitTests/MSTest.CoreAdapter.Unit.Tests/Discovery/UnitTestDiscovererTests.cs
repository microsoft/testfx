// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery
{
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
        private UnitTestDiscoverer unitTestDiscoverer;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        private Mock<IMessageLogger> mockMessageLogger;
        private Mock<ITestCaseDiscoverySink> mockTestCaseDiscoverySink;
        private Mock<IRunSettings> mockRunSettings;
        private Mock<IDiscoveryContext> mockDiscoveryContext;
        private UnitTestElement test;
        private List<UnitTestElement> testElements;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.unitTestDiscoverer = new UnitTestDiscoverer();

            this.mockMessageLogger = new Mock<IMessageLogger>();
            this.mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
            this.mockRunSettings = new Mock<IRunSettings>();

            this.mockDiscoveryContext = new Mock<IDiscoveryContext>();
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);

            this.test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            this.testElements = new List<UnitTestElement> { this.test };
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.test = null;
            this.testElements = null;
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
                this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                    .Returns(source);
                this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                    .Returns(false);
            }

            this.unitTestDiscoverer.DiscoverTests(sources, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object);

            // Assert.
            this.mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethodV1]
        public void DiscoverTestsInSourceShouldSendBackAllWarnings()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
                .Returns(Source);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
                .Returns(false);

            this.unitTestDiscoverer.DiscoverTestsInSource(Source, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object);

            // Assert.
            this.mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Once);
        }

        [TestMethodV1]
        public void DiscoverTestsInSourceShouldSendBackTestCasesDiscovered()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(Source))
                .Returns(Source);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(Source))
                .Returns(true);
            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(
                tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(Source, It.IsAny<bool>()))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);
            this.testablePlatformServiceProvider.MockTestSourceHost.Setup(
                ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
                .Returns(new AssemblyEnumerator());

            this.unitTestDiscoverer.DiscoverTestsInSource(Source, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
        }

        [TestMethodV1]
        public void SendTestCasesShouldNotSendAnyTestCasesIfThereAreNoTestElements()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            // There is a null check for testElements in the code flow before this function call. So not adding a unit test for that.
            this.unitTestDiscoverer.SendTestCases(Source, new List<UnitTestElement> { }, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendAllTestCaseData()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            this.unitTestDiscoverer.SendTestCases(Source, testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
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
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            this.SetupNavigation(Source, this.test, this.test.TestMethod.FullClassName, this.test.TestMethod.Name);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(settingsXml);

            // Act
            MSTestSettings.PopulateSettings(this.mockDiscoveryContext.Object);
            this.unitTestDiscoverer.SendTestCases(Source, this.testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.LineNumber == -1)), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.CodeFilePath == null)), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNavigationData()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            this.SetupNavigation(Source, this.test, this.test.TestMethod.FullClassName, this.test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(Source, this.testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert
            this.VerifyNavigationDataIsPresent();
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNavigationDataWhenDeclaredClassFullNameIsNonNull()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            this.test.TestMethod.DeclaringClassFullName = "DC";

            this.SetupNavigation(Source, this.test, this.test.TestMethod.DeclaringClassFullName, this.test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(Source, this.testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert
            this.VerifyNavigationDataIsPresent();
        }

        [TestMethodV1]
        public void SendTestCasesShouldUseNaigationSessionForDeclaredAssemblyName()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            this.test.TestMethod.DeclaringAssemblyName = "DummyAssembly2.dll";
            ////var test = new UnitTestElement(
            ////    new TestMethod("M", "C", "A", false)
            ////    {
            ////        DeclaringAssemblyName = "DummyAssembly2.dll"
            ////    });

            this.SetupNavigation(Source, this.test, this.test.TestMethod.DeclaringClassFullName, this.test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(Source, this.testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert
            this.testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.CreateNavigationSession("DummyAssembly2.dll"), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNavigationDataForAsyncMethods()
        {
            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(Source))
                .Returns((object)null);

            this.test.AsyncTypeName = "ATN";

            this.SetupNavigation(Source, this.test, this.test.AsyncTypeName, "MoveNext");

            // Act
            this.unitTestDiscoverer.SendTestCases(Source, this.testElements, this.mockTestCaseDiscoverySink.Object, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object);

            // Assert
            this.VerifyNavigationDataIsPresent();
        }

        /// <summary>
        /// Send test cases should send filtered test cases only.
        /// </summary>
        [TestMethodV1]
        public void SendTestCasesShouldSendFilteredTestCasesIfValidFilterExpression()
        {
            TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new TestableDiscoveryContextWithGetTestCaseFilter(() => new TestableTestCaseFilterExpression((p) => (p.DisplayName == "M1")));

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            // Action
            this.unitTestDiscoverer.SendTestCases(Source, testElements, this.mockTestCaseDiscoverySink.Object, discoveryContext, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
        }

        /// <summary>
        /// Send test cases should send all test cases if filter expression is null.
        /// </summary>
        [TestMethodV1]
        public void SendTestCasesShouldSendAllTestCasesIfNullFilterExpression()
        {
            TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new TestableDiscoveryContextWithGetTestCaseFilter(() => null);

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            // Action
            this.unitTestDiscoverer.SendTestCases(Source, testElements, this.mockTestCaseDiscoverySink.Object, discoveryContext, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
        }

        /// <summary>
        /// Send test cases should send all test cases if GetTestCaseFilter method is not present in DiscoveryContext.
        /// </summary>
        [TestMethodV1]
        public void SendTestCasesShouldSendAllTestCasesIfGetTestCaseFilterNotPresent()
        {
            TestableDiscoveryContextWithoutGetTestCaseFilter discoveryContext = new TestableDiscoveryContextWithoutGetTestCaseFilter();

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            // Action
            this.unitTestDiscoverer.SendTestCases(Source, testElements, this.mockTestCaseDiscoverySink.Object, discoveryContext, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
        }

        /// <summary>
        /// Send test cases should not send any test cases if filter parsing error.
        /// </summary>
        [TestMethodV1]
        public void SendTestCasesShouldNotSendAnyTestCasesIfFilterError()
        {
            TestableDiscoveryContextWithGetTestCaseFilter discoveryContext = new TestableDiscoveryContextWithGetTestCaseFilter(() => { throw new TestPlatformFormatException("DummyException"); });

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            // Action
            this.unitTestDiscoverer.SendTestCases(Source, testElements, this.mockTestCaseDiscoverySink.Object, discoveryContext, this.mockMessageLogger.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Never);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Never);
        }

        private void SetupNavigation(string source, UnitTestElement test, string className, string methodName)
        {
            var testNavigationData = new DummyNavigationData("DummyFileName.cs", 1, 10);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns(testNavigationData);
            int minLineNumber = testNavigationData.MinLineNumber;
            string fileName = testNavigationData.FileName;

            this.testablePlatformServiceProvider.MockFileOperations.Setup(
                fo =>
                fo.GetNavigationData(
                    testNavigationData,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    out minLineNumber,
                    out fileName));

            var testCase1 = test.ToTestCase();

            this.SetTestCaseNavigationData(testCase1, testNavigationData.FileName, testNavigationData.MinLineNumber);
        }

        private void SetTestCaseNavigationData(TestCase testCase, string fileName, int lineNumber)
        {
            testCase.LineNumber = lineNumber;
            testCase.CodeFilePath = fileName;
        }

        private void VerifyNavigationDataIsPresent()
        {
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.LineNumber == 1)), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.CodeFilePath.Equals("DummyFileName.cs"))), Times.Once);
        }
    }

    internal class DummyNavigationData
    {
        public DummyNavigationData(string fileName, int minLineNumber, int maxLineNumber)
        {
            this.FileName = fileName;
            this.MinLineNumber = minLineNumber;
            this.MaxLineNumber = maxLineNumber;
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
        private readonly Func<ITestCaseFilterExpression> getFilter;

        public TestableDiscoveryContextWithGetTestCaseFilter(Func<ITestCaseFilterExpression> getFilter)
        {
            this.getFilter = getFilter;
        }

        public IRunSettings RunSettings { get; }

        public ITestCaseFilterExpression GetTestCaseFilter(
            IEnumerable<string> supportedProperties,
            Func<string, TestProperty> propertyProvider)
        {
            return this.getFilter();
        }
    }

    internal class TestableDiscoveryContextWithoutGetTestCaseFilter : IDiscoveryContext
    {
        public IRunSettings RunSettings { get; }
    }

    internal class TestableTestCaseFilterExpression : ITestCaseFilterExpression
    {
        private readonly Func<TestCase, bool> matchTest;

        public TestableTestCaseFilterExpression(Func<TestCase, bool> matchTestCase)
        {
            this.matchTest = matchTestCase;
        }

        public string TestCaseFilterValue { get; }

        public bool MatchTestCase(TestCase testCase, Func<string, object> propertyValueProvider)
        {
            return this.matchTest(testCase);
        }
    }
}
