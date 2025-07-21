// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestExecutorTests : TestContainer
{
    private readonly Mock<IRunContext> _mockRunContext;
    private readonly Mock<IRunSettings> _mockRunSettings;
    private readonly Mock<IFrameworkHandle> _mockFrameworkHandle;
    private readonly MSTestExecutor _mstestExecutor;

    public MSTestExecutorTests()
    {
        _mockRunContext = new();
        _mockRunSettings = new();
        _mockFrameworkHandle = new();
        _mstestExecutor = new MSTestExecutor();
    }

    public void MSTestExecutorShouldProvideTestExecutionUri()
    {
        var testExecutor = new MSTestExecutor();

        var extensionUriString = (ExtensionUriAttribute)testExecutor.GetType().GetCustomAttributes(typeof(ExtensionUriAttribute), false).Single();

        Verify(extensionUriString.ExtensionUri == EngineConstants.ExecutorUriString);
    }

    public async Task RunTestsShouldNotExecuteTestsIfTestSettingsIsGiven()
    {
        var testCase = new TestCase("DummyName", new Uri("executor://MSTestAdapter/v4"), Assembly.GetExecutingAssembly().Location);
        TestCase[] tests = [testCase];
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <ForcedLegacyMode>true</ForcedLegacyMode>
                <IgnoreTestImpact>true</IgnoreTestImpact>
              </MSTest>
            </RunSettings>
            """;
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        await _mstestExecutor.RunTestsAsync(tests, _mockRunContext.Object, _mockFrameworkHandle.Object, null);

        // Test should not start if TestSettings is given.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
    }

    public async Task RunTestsShouldReportErrorAndBailOutOnSettingsException()
    {
        var testCase = new TestCase("DummyName", new Uri("executor://MSTestAdapter/v4"), Assembly.GetExecutingAssembly().Location);
        TestCase[] tests = [testCase];
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
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        // Act.
        await _mstestExecutor.RunTestsAsync(tests, _mockRunContext.Object, _mockFrameworkHandle.Object, null);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(tests[0]), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public async Task RunTestsWithSourcesShouldNotExecuteTestsIfTestSettingsIsGiven()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
        string runSettingsXml =
            """
            <RunSettings>
              <MSTest>
                <SettingsFile>DummyPath\\TestSettings1.testsettings</SettingsFile>
                <ForcedLegacyMode>true</ForcedLegacyMode>
                <IgnoreTestImpact>true</IgnoreTestImpact>
              </MSTest>
            </RunSettings>
            """;
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);
        await _mstestExecutor.RunTestsAsync(sources, _mockRunContext.Object, _mockFrameworkHandle.Object, null);

        // Test should not start if TestSettings is given.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
    }

    public async Task RunTestsWithSourcesShouldReportErrorAndBailOutOnSettingsException()
    {
        var sources = new List<string> { Assembly.GetExecutingAssembly().Location };
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
        _mockRunContext.Setup(dc => dc.RunSettings).Returns(_mockRunSettings.Object);
        _mockRunSettings.Setup(rs => rs.SettingsXml).Returns(runSettingsXml);

        // Act.
        await _mstestExecutor.RunTestsAsync(sources, _mockRunContext.Object, _mockFrameworkHandle.Object, null);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }
}
