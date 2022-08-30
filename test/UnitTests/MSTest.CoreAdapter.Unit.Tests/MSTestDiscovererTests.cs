// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

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
    private TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private Mock<IMessageLogger> _mockMessageLogger;
    private Mock<ITestCaseDiscoverySink> _mockTestCaseDiscoverySink;
    private Mock<IDiscoveryContext> _mockDiscoveryContext;
    private Mock<IRunSettings> _mockRunSettings;
    private MSTestDiscoverer _discoverer;

    [TestInitialize]
    public void TestInit()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        _mockMessageLogger = new Mock<IMessageLogger>();
        _mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
        _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _discoverer = new MSTestDiscoverer();

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    [TestCleanup]
    public void Cleanup() => PlatformServiceProvider.Instance = null;

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
        Assert.AreEqual(1, attributes.Count(attribute => attribute.FileExtension == ".xap"));
    }

    [TestMethod]
    public void MSTestDiscovererHasAppxAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
        Assert.IsNotNull(attributes);
        Assert.AreEqual(1, attributes.Count(attribute => attribute.FileExtension == ".appx"));
    }

    [TestMethod]
    public void MSTestDiscovererHasDllAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
        Assert.IsNotNull(attributes);
        Assert.AreEqual(1, attributes.Count(attribute => attribute.FileExtension == ".dll"));
    }

    [TestMethod]
    public void MSTestDiscovererHasExeAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetTypeInfo().GetCustomAttributes(typeof(FileExtensionAttribute)).Cast<FileExtensionAttribute>();
        Assert.IsNotNull(attributes);
        Assert.AreEqual(1, attributes.Count(attribute => attribute.FileExtension == ".exe"));
    }

    [TestMethod]
    public void DiscoverTestsShouldThrowIfSourcesIsNull()
    {
        void a() => _discoverer.DiscoverTests(null, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void DiscoverTestsShouldThrowIfDiscoverySinkIsNull()
    {
        void a() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, null);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void DiscoverTestsShouldThrowIfLoggerIsNull()
    {
        void a() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, null, _mockTestCaseDiscoverySink.Object);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void DiscoverTestsShouldThrowIfSourcesAreNotValid()
    {
        // Setup Mocks.
        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { });

        void a() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(NotSupportedException));
    }

    [TestMethod]
    public void DiscoverTestsShouldNotThrowIfdiscoveryContextIsNull()
    {
        var source = Assembly.GetExecutingAssembly().Location;

        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { ".dll" });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
            .Returns(source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
            .Returns(true);

        // This call should not throw a null reference exception.
        _discoverer.DiscoverTests(new List<string> { source }, null, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
    }

    [TestMethod]
    public void DiscoverTestsShouldDiscoverTests()
    {
        var source = Assembly.GetExecutingAssembly().Location;

        // Setup mocks.
        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { ".dll" });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
            .Returns(source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
            .Returns(true);
        _testablePlatformServiceProvider.MockTestSourceValidator.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.CreateNavigationSession(source))
            .Returns((object)null);
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
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
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

        var source = Assembly.GetExecutingAssembly().Location;
        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
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
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingxml);
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

        var source = Assembly.GetExecutingAssembly().Location;
        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        _mockMessageLogger.Verify(fh => fh.SendMessage(TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    [TestMethod]
    public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
    {
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null);
        MSTestDiscoverer discoverer = new();

        void a() => discoverer.AreValidSources(new List<string> { "dummy" });
        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
    }

    [TestMethod]
    public void AreValidSourcesShouldReturnFalseIfValidSourceExtensionsIsEmpty()
    {
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { });
        MSTestDiscoverer discoverer = new();

        Assert.IsFalse(discoverer.AreValidSources(new List<string> { "dummy.te" }));
    }

    [TestMethod]
    public void AreValidSourcesShouldReturnTrueForValidSourceExtensions()
    {
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".te" });
        MSTestDiscoverer discoverer = new();

        Assert.IsTrue(discoverer.AreValidSources(new List<string> { "dummy.te" }));
    }

    [TestMethod]
    public void AreValidSourcesShouldReturnFalseForInvalidSourceExtensions()
    {
        _testablePlatformServiceProvider.MockTestSourceValidator.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".nte", ".tep" });
        MSTestDiscoverer discoverer = new();

        Assert.IsFalse(discoverer.AreValidSources(new List<string> { "dummy.te" }));
    }
}
