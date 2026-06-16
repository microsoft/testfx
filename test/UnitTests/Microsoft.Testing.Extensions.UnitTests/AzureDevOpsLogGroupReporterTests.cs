// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsLogGroupReporterTests
{
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IOutputDevice> _outputDeviceMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly List<IOutputDeviceData> _outputData = [];

    public AzureDevOpsLogGroupReporterTests()
    {
        _ = _testApplicationModuleInfoMock.Setup(info => info.TryGetAssemblyName()).Returns("MyAssembly");
        _ = _loggerFactoryMock.Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _ = _outputDeviceMock
            .Setup(outputDevice => outputDevice.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _outputData.Add(data))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenAzureDevOpsOptionNotSetAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: false, tfBuild: true);
        Assert.IsFalse(await reporter.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenTfBuildNotSetAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: true, tfBuild: false);
        Assert.IsFalse(await reporter.IsEnabledAsync());
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue_WhenAzureDevOpsOptionSetAndTfBuildSetAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: true, tfBuild: true);
        Assert.IsTrue(await reporter.IsEnabledAsync());
    }

    [TestMethod]
    public async Task SessionStarting_EmitsGroupHeaderWithAssemblyAndTfmAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: true, tfBuild: true);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext());

        string[] lines = GetFormattedLines();
        Assert.HasCount(1, lines);
        Assert.StartsWith("##[group]Tests: MyAssembly (", lines[0]);
    }

    [TestMethod]
    public async Task SessionFinishing_EmitsEndGroup_WhenGroupWasOpenedAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: true, tfBuild: true);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext());
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext());

        string[] lines = GetFormattedLines();
        Assert.HasCount(2, lines);
        Assert.StartsWith("##[group]", lines[0]);
        Assert.AreEqual("##[endgroup]", lines[1]);
    }

    [TestMethod]
    public async Task SessionFinishing_DoesNothing_WhenGroupWasNeverOpenedAsync()
    {
        AzureDevOpsLogGroupReporter reporter = CreateReporter(enabled: true, tfBuild: true);

        await reporter.OnTestSessionFinishingAsync(new TestSessionContext());

        Assert.IsEmpty(GetFormattedLines());
    }

    private AzureDevOpsLogGroupReporter CreateReporter(bool enabled, bool tfBuild)
    {
        Dictionary<string, string[]> options = enabled
            ? new Dictionary<string, string[]> { [AzureDevOpsCommandLineOptions.AzureDevOpsOptionName] = [] }
            : [];
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns(tfBuild ? "true" : null);
        return new AzureDevOpsLogGroupReporter(
            new TestCommandLineOptions(options),
            _environmentMock.Object,
            _outputDeviceMock.Object,
            _testApplicationModuleInfoMock.Object,
            _loggerFactoryMock.Object);
    }

    private string[] GetFormattedLines()
        => [.. _outputData.OfType<FormattedTextOutputDeviceData>().Select(output => output.Text)];

    private sealed class TestSessionContext : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }
}
