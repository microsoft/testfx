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
        private UnitTestDiscoverer unitTestDiscoverer;

        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        private Mock<IMessageLogger> mockMessageLogger;
        private Mock<ITestCaseDiscoverySink> mockTestCaseDiscoverySink;
        private Mock<IRunSettings> mockRunSettings;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            this.unitTestDiscoverer = new UnitTestDiscoverer();

            this.mockMessageLogger = new Mock<IMessageLogger>();
            this.mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
            this.mockRunSettings = new Mock<IRunSettings>();

            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
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

            this.unitTestDiscoverer.DiscoverTests(sources, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockRunSettings.Object);

            // Assert.
            this.mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethodV1]
        public void DiscoverTestsInSourceShouldSendBackAllWarnings()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                .Returns(source);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                .Returns(false);

            this.unitTestDiscoverer.DiscoverTestsInSource(source, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockRunSettings.Object);

            // Assert.
            this.mockMessageLogger.Verify(lm => lm.SendMessage(TestMessageLevel.Warning, It.IsAny<string>()), Times.Once);
        }

        [TestMethodV1]
        public void DiscoverTestsInSourceShouldSendBackTestCasesDiscovered()
        {
            var source = Assembly.GetExecutingAssembly().FullName;

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                .Returns(source);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                .Returns(true);
            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(
                tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), source)).Returns(true);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source, It.IsAny<bool>()))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);
            this.testablePlatformServiceProvider.MockTestSourceHost.Setup(
                ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
                .Returns(new AssemblyEnumerator());

            this.unitTestDiscoverer.DiscoverTestsInSource(source, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockRunSettings.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
        }

        [TestMethodV1]
        public void SendTestCasesShouldNotSendAnyTestCasesIfThereAreNoTestElements()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            // There is a null check for testElements in the code flow before this function call. So not adding a unit test for that.
            this.unitTestDiscoverer.SendTestCases(source, new List<UnitTestElement> { }, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendAllTestCaseData()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };

            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNaigationData()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            var test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            var testElements = new List<UnitTestElement> { test };

            this.SetupNavigation(source, test, test.TestMethod.FullClassName, test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M")), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNaigationDataWhenDeclaredClassFullNameIsNonNull()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            var test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            test.TestMethod.DeclaringClassFullName = "DC";
            var testElements = new List<UnitTestElement> { test };

            this.SetupNavigation(source, test, test.TestMethod.DeclaringClassFullName, test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M")), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldUseNaigationSessionForDeclaredAssemblyName()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            var test = new UnitTestElement(
                new TestMethod("M", "C", "A", false)
                {
                    DeclaringAssemblyName = "DummyAssembly2.dll"
                });

            var testElements = new List<UnitTestElement> { test };

            this.SetupNavigation(source, test, test.TestMethod.DeclaringClassFullName, test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.CreateNavigationSession("DummyAssembly2.dll"), Times.Once);
        }

        [TestMethodV1]
        public void SendTestCasesShouldSendTestCasesWithNaigationDataForAsyncMethods()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);

            var test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            test.AsyncTypeName = "ATN";
            var testElements = new List<UnitTestElement> { test };

            this.SetupNavigation(source, test, test.AsyncTypeName, "MoveNext");

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M")), Times.Once);
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
            IRunSettings runSettings)
        {
            var testCase1 = new TestCase("A", new System.Uri("executor://testExecutor"), source);
            var testCase2 = new TestCase("B", new System.Uri("executor://testExecutor"), source);
            discoverySink.SendTestCase(testCase1);
            discoverySink.SendTestCase(testCase2);
        }
    }
}