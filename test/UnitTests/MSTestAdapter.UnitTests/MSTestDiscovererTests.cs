// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestDiscovererTests : TestContainer
{
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    private readonly Mock<IMessageLogger> _mockMessageLogger;
    private readonly Mock<ITestCaseDiscoverySink> _mockTestCaseDiscoverySink;
    private readonly Mock<IDiscoveryContext> _mockDiscoveryContext;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler;
    private readonly MSTestDiscoverer _discoverer;

    public MSTestDiscovererTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();

        _mockMessageLogger = new Mock<IMessageLogger>();
        _mockTestCaseDiscoverySink = new Mock<ITestCaseDiscoverySink>();
        _mockDiscoveryContext = new Mock<IDiscoveryContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _mockTestSourceHandler = new();
        _discoverer = new MSTestDiscoverer(_mockTestSourceHandler.Object);

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public void MSTestDiscovererHasMSTestAdapterAsExecutorUri()
    {
        DefaultExecutorUriAttribute attribute = typeof(MSTestDiscoverer).GetCustomAttributes<DefaultExecutorUriAttribute>().First();
        Verify(attribute is not null);
        Verify(attribute.ExecutorUri == "executor://MSTestAdapter/v4");
    }

    public void MSTestDiscovererHasXapAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetCustomAttributes<FileExtensionAttribute>();
        Verify(attributes is not null);
        Verify(attributes.Count(attribute => attribute.FileExtension == ".xap") == 1);
    }

    public void MSTestDiscovererHasAppxAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetCustomAttributes<FileExtensionAttribute>();
        Verify(attributes is not null);
        Verify(attributes.Count(attribute => attribute.FileExtension == ".appx") == 1);
    }

    public void MSTestDiscovererHasDllAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetCustomAttributes<FileExtensionAttribute>();
        Verify(attributes is not null);
        Verify(attributes.Count(attribute => attribute.FileExtension == ".dll") == 1);
    }

    public void MSTestDiscovererHasExeAsFileExtension()
    {
        IEnumerable<FileExtensionAttribute> attributes = typeof(MSTestDiscoverer).GetCustomAttributes<FileExtensionAttribute>();
        Verify(attributes is not null);
        Verify(attributes.Count(attribute => attribute.FileExtension == ".exe") == 1);
    }

    public void DiscoverTestsShouldThrowIfSourcesIsNull()
    {
        void A() => _discoverer.DiscoverTests(null!, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
        VerifyThrows<ArgumentNullException>(A);
    }

    public void DiscoverTestsShouldThrowIfDiscoverySinkIsNull()
    {
        void A() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, null!);
        VerifyThrows<ArgumentNullException>(A);
    }

    public void DiscoverTestsShouldThrowIfLoggerIsNull()
    {
        void A() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, null!, _mockTestCaseDiscoverySink.Object);
        VerifyThrows<ArgumentNullException>(A);
    }

    public void DiscoverTestsShouldThrowIfSourcesAreNotValid()
    {
        // Setup Mocks.
        _mockTestSourceHandler.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { });

        void A() => _discoverer.DiscoverTests(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
        VerifyThrows<NotSupportedException>(A);
    }

    public void DiscoverTestsShouldNotThrowIfDiscoveryContextIsNull()
    {
        string source = GetCurrentAssembly();

        _mockTestSourceHandler.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { ".dll" });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
            .Returns(source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
            .Returns(true);

        // This call should not throw a null reference exception.
        _discoverer.DiscoverTests(new List<string> { source }, null!, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);
    }

    public void DiscoverTestsShouldDiscoverTests()
    {
        string source = GetCurrentAssembly();

        // Setup mocks.
        _mockTestSourceHandler.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { ".dll" });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
            .Returns(source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
            .Returns(true);
        _mockTestSourceHandler.Setup(
            tsv => tsv.IsAssemblyReferenced(It.IsAny<AssemblyName>(), source)).Returns(true);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source, It.IsAny<bool>()))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
    }

    private static string GetCurrentAssembly()
        => Assembly.GetExecutingAssembly().Location.Replace(".exe", ".dll");

    public void DiscoveryShouldNotHappenIfTestSettingsIsGiven()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <IgnoreTestImpact>true</IgnoreTestImpact>
              </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

        string source = GetCurrentAssembly();
        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
    }

    public void DiscoveryShouldReportAndBailOutOnSettingsException()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <Parallelize>
                  <Scope>Pond</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """;
        _mockDiscoveryContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".dll" });

        string source = GetCurrentAssembly();
        _discoverer.DiscoverTests(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        _mockMessageLogger.Verify(fh => fh.SendMessage(TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null!);
        var sources = new List<string> { "dummy" };
        VerifyThrows<ArgumentNullException>(() => MSTestDiscovererHelpers.AreValidSources(sources, _mockTestSourceHandler.Object));
    }

    public void AreValidSourcesShouldReturnFalseIfValidSourceExtensionsIsEmpty()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { });
        Verify(!MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object));
    }

    public void AreValidSourcesShouldReturnTrueForValidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".te" });
        Verify(MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object));
    }

    public void AreValidSourcesShouldReturnFalseForInvalidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".nte", ".tep" });
        Verify(!MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object));
    }

    [TestClass]
    public class DummyClass
    {
        [TestMethod]
        public void DummyTestMethod()
        {
            // This is a dummy test method to ensure that the MSTestDiscoverer can discover tests.
            // It does not perform any assertions or operations.
        }
    }
}
