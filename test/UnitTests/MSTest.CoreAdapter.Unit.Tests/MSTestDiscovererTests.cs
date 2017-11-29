// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests
{
    extern alias FrameworkV1;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using global::MSTestAdapter.TestUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    using Moq;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestCleanup = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestCleanupAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

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

        [TestCleanup]
        public void Cleanup()
        {
            PlatformServiceProvider.Instance = null;
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
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfDiscoverySinkIsNull()
        {
            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, null);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfLoggerIsNull()
        {
            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, null, this.mockTestCaseDiscoverySink.Object);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        [TestMethod]
        public void DiscoverTestsShouldThrowIfSourcesAreNotValid()
        {
            // Setup Mocks.
            this.testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
                .Returns(new List<string> { });

            Action a = () => this.discoverer.DiscoverTests(new List<string>(), this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(NotSupportedException));
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
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source, It.IsAny<bool>()))
                .Returns(Assembly.GetExecutingAssembly());
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
                .Returns((object)null);
            this.testablePlatformServiceProvider.MockTestSourceHost.Setup(
                ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
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
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

            var source = Assembly.GetExecutingAssembly().Location;
            this.discoverer.DiscoverTests(new List<string> { source }, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        }

        [TestMethod]
        public void DiscoveryShouldReportAndBailOutOnSettingsException()
        {
            string runSettingxml =
            @"<RunSettings>   
			        <MSTest>   
				        <Parallelize>
				          <Scope>Pond</Scope>
				        </Parallelize>
			        </MSTest>
		    </RunSettings>";
            this.mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(this.mockRunSettings.Object);
            this.mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

            var source = Assembly.GetExecutingAssembly().Location;
            this.discoverer.DiscoverTests(new List<string> { source }, this.mockDiscoveryContext.Object, this.mockMessageLogger.Object, this.mockTestCaseDiscoverySink.Object);

            // Assert.
            this.mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
            this.mockMessageLogger.Verify(fh => fh.SendMessage(TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
        }

        [TestMethod]
        public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
        {
            this.testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null);
            MSTestDiscoverer discoverer = new MSTestDiscoverer();

            Action a = () => discoverer.AreValidSources(new List<string> { "dummy" });
            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
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
