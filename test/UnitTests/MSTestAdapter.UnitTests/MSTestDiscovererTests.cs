// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

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

    public async Task DiscoverTestsShouldThrowIfSourcesIsNull()
    {
        Func<Task> action = async () => await _discoverer.DiscoverTestsAsync(null!, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    public async Task DiscoverTestsShouldThrowIfDiscoverySinkIsNull()
    {
        Func<Task> action = async () => await _discoverer.DiscoverTestsAsync(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, (ITestCaseDiscoverySink)null!, configuration: null, isMTP: false);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    public async Task DiscoverTestsShouldThrowIfLoggerIsNull()
    {
        Func<Task> action = async () => await _discoverer.DiscoverTestsAsync(new List<string>(), _mockDiscoveryContext.Object, null!, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    public async Task DiscoverTestsShouldThrowIfSourcesAreNotValid()
    {
        // Setup Mocks.
        _mockTestSourceHandler.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { });

        Func<Task> action = async () => await _discoverer.DiscoverTestsAsync(new List<string>(), _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);
        await action.Should().ThrowAsync<NotSupportedException>();
    }

    public async Task DiscoverTestsShouldNotThrowIfDiscoveryContextIsNull()
    {
        string source = GetCurrentAssembly();

        _mockTestSourceHandler.Setup(tsv => tsv.ValidSourceExtensions)
            .Returns(new List<string> { ".dll" });
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.GetFullFilePath(source))
            .Returns(source);
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.DoesFileExist(source))
            .Returns(true);

        // This call should not throw a null reference exception.
        await _discoverer.DiscoverTestsAsync(new List<string> { source }, null!, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);
    }

    public async Task DiscoverTestsShouldDiscoverTests()
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
        _testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(source))
            .Returns(Assembly.GetExecutingAssembly());
        _testablePlatformServiceProvider.MockTestSourceHost.Setup(
            ih => ih.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns(new AssemblyEnumerator());

        await _discoverer.DiscoverTestsAsync(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.AtLeastOnce);
    }

    private static string GetCurrentAssembly()
        => Assembly.GetExecutingAssembly().Location.Replace(".exe", ".dll");

    public async Task DiscoveryShouldReportAndBailOutOnSettingsException()
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
        await _discoverer.DiscoverTestsAsync(new List<string> { source }, _mockDiscoveryContext.Object, _mockMessageLogger.Object, _mockTestCaseDiscoverySink.Object, configuration: null, isMTP: false);

        // Assert.
        _mockTestCaseDiscoverySink.Verify(ds => ds.SendTestCase(It.IsAny<TestCase>()), Times.Never);
        _mockMessageLogger.Verify(fh => fh.SendMessage(TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null!);
        var sources = new List<string> { "dummy" };
        Action action = () => MSTestDiscovererHelpers.AreValidSources(sources, _mockTestSourceHandler.Object);
        action.Should().Throw<ArgumentNullException>();
    }

    public void AreValidSourcesShouldReturnFalseIfValidSourceExtensionsIsEmpty()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeFalse();
    }

    public void AreValidSourcesShouldReturnTrueForValidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".te" });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeTrue();
    }

    public void AreValidSourcesShouldReturnFalseForInvalidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".nte", ".tep" });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeFalse();
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
