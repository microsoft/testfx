// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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
        _mockRunContext = new Mock<IRunContext>();
        _mockRunSettings = new Mock<IRunSettings>();
        _mockFrameworkHandle = new Mock<IFrameworkHandle>();
        _mstestExecutor = new MSTestExecutor();
    }

    public void MSTestExecutorShouldProvideTestExecutionUri()
    {
        var testExecutor = new MSTestExecutor();

        var extensionUriString = (ExtensionUriAttribute)testExecutor.GetType().GetCustomAttributes(typeof(ExtensionUriAttribute), false).Single();

        extensionUriString.ExtensionUri.Should().Be(EngineConstants.ExecutorUriString);
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
        await _mstestExecutor.RunTestsAsync(sources, _mockRunContext.Object, _mockFrameworkHandle.Object, null, isMTP: false);

        // Assert.
        _mockFrameworkHandle.Verify(fh => fh.RecordStart(It.IsAny<TestCase>()), Times.Never);
        _mockFrameworkHandle.Verify(fh => fh.SendMessage(TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, "Invalid value 'Pond' specified for 'Scope'. Supported scopes are ClassLevel, MethodLevel."), Times.Once);
    }

    public void LegacyEntryPointsRejectInvalidInputsWithExactParameterNames()
    {
        Action<IEnumerable<TestCase>?, IRunContext?, IFrameworkHandle?> runCases = GetLegacyEntryPoint<TestCase>();
        Action<IEnumerable<string>?, IRunContext?, IFrameworkHandle?> runSources = GetLegacyEntryPoint<string>();
        Action missingCases = () => runCases(null, null, _mockFrameworkHandle.Object);
        Action missingSources = () => runSources(null, null, _mockFrameworkHandle.Object);
        Action missingCaseHandle = () => runCases(new[] { new TestCase("Type.Method", EngineConstants.ExecutorUri, "source.dll") }, null, null);
        Action missingSourceHandle = () => runSources(new[] { "source.dll" }, null, null);
        Action emptyCases = () => runCases(Array.Empty<TestCase>(), null, _mockFrameworkHandle.Object);
        Action emptySources = () => runSources(Array.Empty<string>(), null, _mockFrameworkHandle.Object);

        missingCases.Should().ThrowExactly<ArgumentNullException>().WithParameterName("tests");
        missingSources.Should().ThrowExactly<ArgumentNullException>().WithParameterName("sources");
        missingCaseHandle.Should().ThrowExactly<ArgumentNullException>().WithParameterName("frameworkHandle");
        missingSourceHandle.Should().ThrowExactly<ArgumentNullException>().WithParameterName("frameworkHandle");
        emptyCases.Should().ThrowExactly<ArgumentException>().WithParameterName("tests");
        emptySources.Should().ThrowExactly<ArgumentException>().WithParameterName("sources");
        _mockFrameworkHandle.Verify(value => value.RecordResult(It.IsAny<ObjectModel.TestResult>()), Times.Never);
    }

    private Action<IEnumerable<T>?, IRunContext?, IFrameworkHandle?> GetLegacyEntryPoint<T>()
    {
        // Exercise the retained binary API, not a recommended source-level call to an obsolete method.
        MethodInfo? method = typeof(MSTestExecutor).GetMethod(
            nameof(MSTestExecutor.RunTests),
            [typeof(IEnumerable<T>), typeof(IRunContext), typeof(IFrameworkHandle)]);
        method.Should().NotBeNull();

        return (Action<IEnumerable<T>?, IRunContext?, IFrameworkHandle?>)method!.CreateDelegate(
            typeof(Action<IEnumerable<T>?, IRunContext?, IFrameworkHandle?>), _mstestExecutor);
    }

    public async Task SelectedCasesExecuteThroughTheNeutralEngineAndPreserveHostRecording()
    {
        IPlatformServiceProvider previous = PlatformServiceProvider.Instance;
        try
        {
            PlatformServiceProvider.Instance = null!;
            var executor = new MSTestExecutor(CancellationToken.None);
            Type fixture = typeof(MSTestDiscovererTests.DummyClass);
            var testCase = new TestCase(fixture.FullName + ".DummyTestMethod", EngineConstants.ExecutorUri, fixture.Assembly.Location);
            testCase.SetPropertyValue(AdapterTestProperties.TestClassNameProperty, fixture.FullName);
            testCase.SetPropertyValue(MSTest.TestAdapter.Extensions.TestCaseExtensions.ManagedTypeProperty, fixture.FullName);
            testCase.SetPropertyValue(MSTest.TestAdapter.Extensions.TestCaseExtensions.ManagedMethodProperty, "DummyTestMethod");
            _mockRunContext.Setup(value => value.RunSettings).Returns(_mockRunSettings.Object);
            _mockRunSettings.SetupGet(value => value.SettingsXml).Returns("<RunSettings><MSTest><DeploymentEnabled>false</DeploymentEnabled></MSTest></RunSettings>");
            List<ObjectModel.TestResult> results = [];
            _mockFrameworkHandle.Setup(value => value.RecordResult(It.IsAny<ObjectModel.TestResult>()))
                .Callback<ObjectModel.TestResult>(results.Add);

            await executor.RunTestsAsync(new[] { testCase }, _mockRunContext.Object, _mockFrameworkHandle.Object, null);
            executor.Cancel();

            results.Should().ContainSingle();
            results[0].TestCase.Should().BeSameAs(testCase);
            results[0].Outcome.Should().Be(TestOutcome.Passed, "nested diagnostic: {0}", results[0].ErrorMessage);
            _mockFrameworkHandle.Verify(value => value.RecordStart(testCase), Times.Once);
            _mockFrameworkHandle.Verify(value => value.RecordEnd(testCase, TestOutcome.Passed), Times.Once);
        }
        finally
        {
            MSTestSettings.Reset();
            PlatformServiceProvider.Instance = previous;
        }
    }
}
