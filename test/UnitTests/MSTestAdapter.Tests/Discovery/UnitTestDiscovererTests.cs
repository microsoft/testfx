// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="UnitTestDiscovererTests.cs">
//   
// </copyright>
// 
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery
{
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
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Navigation;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using Moq;

    [TestClass]
    public class UnitTestDiscovererTests
    {
        private static readonly AssemblyName UnitTestFrameworkAssemblyName = typeof(TestMethodAttribute).GetTypeInfo().Assembly.GetName();

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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);
            this.testablePlatformServiceProvider.MockTestSourceHost.Setup(
                ih => ih.CreateInstanceForType(It.IsAny<Type>(), null, It.IsAny<string>(), It.IsAny<IRunSettings>()))
                .Returns(new AssemblyEnumerator());

            this.unitTestDiscoverer.DiscoverTestsInSource(source, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object, this.mockRunSettings.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void SendTestCasesShouldNotSendAnyTestCasesIfThereAreNoTestElements()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);

            // There is a null check for testElements in the code flow before this function call. So not adding a unit test for that.
            this.unitTestDiscoverer.SendTestCases(source, new List<UnitTestElement> { }, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethod]
        public void SendTestCasesShouldSendAllTestCaseData()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);

            var test1 = new UnitTestElement(new TestMethod("M1", "C", "A", false));
            var test2 = new UnitTestElement(new TestMethod("M2", "C", "A", false));
            var testElements = new List<UnitTestElement> { test1, test2 };
            
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);
            
            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M1")), Times.Once);
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M2")), Times.Once);
        }

        [TestMethod]
        public void SendTestCasesShouldSendTestCasesWithNaigationData()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);

            var test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            var testElements = new List<UnitTestElement> { test };
            
            this.SetupNavigation(source, test, test.TestMethod.FullClassName, test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M")), Times.Once);
        }

        [TestMethod]
        public void SendTestCasesShouldSendTestCasesWithNaigationDataWhenDeclaredClassFullNameIsNonNull()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);

            var test = new UnitTestElement(new TestMethod("M", "C", "A", false));
            test.TestMethod.DeclaringClassFullName = "DC";
            var testElements = new List<UnitTestElement> { test };

            this.SetupNavigation(source, test, test.TestMethod.DeclaringClassFullName, test.TestMethod.Name);

            // Act
            this.unitTestDiscoverer.SendTestCases(source, testElements, this.mockTestCaseDiscoverySink.Object);

            // Assert
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.Is<TestCase>(tc => tc.FullyQualifiedName == "C.M")), Times.Once);
        }

        [TestMethod]
        public void SendTestCasesShouldSendTestCasesWithNaigationDataForAsyncMethods()
        {
            var source = "DummyAssembly.dll";

            // Setup mocks.
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((INavigationSession)null);

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

            Mock<INavigationSession> mockNavigationSession = new Mock<INavigationSession>();

            mockNavigationSession.Setup(
                ns => ns.GetNavigationDataForMethod(className, methodName))
                .Returns(testNavigationData);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns(mockNavigationSession.Object);
            var testCase1 = test.ToTestCase();

            this.SetTestCaseNavigationData(testCase1, testNavigationData.FileName, testNavigationData.MinLineNumber);
        }

        private void SetTestCaseNavigationData(TestCase testCase, string fileName, int lineNumber)
        {
            testCase.LineNumber = lineNumber;
            testCase.CodeFilePath = fileName;
        }
    }

    internal class DummyNavigationData : INavigationData
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
}