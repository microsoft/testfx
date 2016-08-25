// --------------------------------------------------------------------------------------------------------------------
// <copyright company="" file="MSTestDiscovererTests.cs">
//   
// </copyright>
// 
// --------------------------------------------------------------------------------------------------------------------
namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Navigation;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using Moq;

    [TestClass]
    public class MSTestDiscovererTests
    {
        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        private Mock<IMessageLogger> mockMessageLogger;
        private Mock<ITestCaseDiscoverySink> mockTestCaseDiscoverySink;
        private Mock<IDiscoveryContext> mockDiscoveryContext;
        private Mock<IRunSettings> mockRunSettings;
        private MSTestDiscoverer discoverer;

        [TestInitialize]
        public void TestInit()
        {
            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();

            this.mockMessageLogger = new Mock<IMessageLogger>();
            this.mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
            this.mockDiscoveryContext = new Mock<IDiscoveryContext>();
            this.mockRunSettings = new Mock<IRunSettings>();
            this.discoverer = new MSTestDiscoverer();

            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;
        }

        [TestMethod]
        public void MSTestDiscovererHasMSTestAdapterAsExecutorUri()
        {
            DefaultExecutorUriAttribute attribute = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(DefaultExecutorUriAttribute)).Cast<DefaultExecutorUriAttribute>().First();
            Assert.IsNotNull(attribute);
            Assert.AreEqual("executor://MSTestAdapter/v2", attribute.ExecutorUri);
        }

        [TestMethod]
        public void MSTestDiscovererHasXapAsFileExtension()
        {
            IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Where(attribute => attribute.FileExtension == ".xap").Count());
        }

        [TestMethod]
        public void MSTestDiscovererHasAppxAsFileExtension()
        {
            IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Where(attribute => attribute.FileExtension == ".appx").Count());
        }

        [TestMethod]
        public void MSTestDiscovererHasDllAsFileExtension()
        {
            IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Where(attribute => attribute.FileExtension == ".dll").Count());
        }

        [TestMethod]
        public void MSTestDiscovererHasExeAsFileExtension()
        {
            IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
            Assert.IsNotNull(attributes);
            Assert.AreEqual(1, attributes.Where(attribute => attribute.FileExtension == ".exe").Count());
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfSourcesIsNull()
        {
            Action a = () => this.discoverer.DiscoverTests(null, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);
            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfDiscoverySinkIsNull()
        {
            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, null);
            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfLoggerIsNull()
        {
            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, null, this.mockTestCaseDiscoverySink.Object);
            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfSourcesAreNotValid()
        {
            // Setup Mocks.
            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
                .Returns(new List<string> { });

            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);
            Assert.ThrowsException<NotSupportedException>(a);
        }

        [TestMethod]
        public void DiscoverTestsShouldNotThrowIfdiscoveryContextIsNull()
        {
            var source = Assembly.GetExecutingAssembly().Location;

            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
                .Returns(new List<string> { ".dll" });
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
                .Returns(source);
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
                .Returns(true);

            // This call should not throw a null reference exception.
            this.discoverer.DiscoverTests(new List<string> { source }, null, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);
        }

        [TestMethod]
        public void DiscoverTestsShouldDiscoverTests()
        {
            var source = Assembly.GetExecutingAssembly().Location;

            // Setup mocks.
            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
                .Returns(new List<string> { ".dll" });
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

            this.discoverer.DiscoverTests(new List<string> { source }, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void DiscoveryShouldNotHappenIfTestSettingsIsGiven()
        {
            string runSettingxml =
	        @"<RunSettings>   
			        <MSTest>   
				        <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
				        <ForcedLegacyMode>true</ForcedLegacyMode>    
				        <IgnoreTestImpact>true</IgnoreTestImpact>  
			        </MSTest>
		    </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);

            var source = Assembly.GetExecutingAssembly().Location;
            this.discoverer.DiscoverTests(new List<string> { source }, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethod]
        public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
        {
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null);
            MSTestDiscoverer discoverer = new MSTestDiscoverer();

            Action a = () => discoverer.AreValidSources(new List<string> { "dummy" });
            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void AreValidSourcesShouldReturnFalseIfValidSourceExtensionsIsEmpty()
        {
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { });
            MSTestDiscoverer discoverer = new MSTestDiscoverer();

            Assert.IsFalse(discoverer.AreValidSources(new List<string> { "dummy.te" }));
        }

        [TestMethod]
        public void AreValidSourcesShouldReturnTrueForValidSourceExtensions()
        {
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".te" });
            MSTestDiscoverer discoverer = new MSTestDiscoverer();

            Assert.IsTrue(discoverer.AreValidSources(new List<string> { "dummy.te" }));
        }

        [TestMethod]
        public void AreValidSourcesShouldReturnFalseForInvalidSourceExtensions()
        {
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".nte", ".tep" });
            MSTestDiscoverer discoverer = new MSTestDiscoverer();

            Assert.IsFalse(discoverer.AreValidSources(new List<string> { "dummy.te" }));
        }
    }
}
