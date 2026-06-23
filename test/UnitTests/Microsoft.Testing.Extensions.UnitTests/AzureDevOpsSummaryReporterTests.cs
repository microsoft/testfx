// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AzureDevOpsSummaryReporterTests
{
    private static readonly string ResultsDirectory = Path.Combine(Path.GetTempPath(), "azdo-summary-reporter-tests");

    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<IEnvironment> _environmentMock = new();
    private readonly Mock<IFileSystem> _fileSystemMock = new();
    private readonly Mock<IOutputDevice> _outputDeviceMock = new();
    private readonly Mock<ITestApplicationModuleInfo> _testApplicationModuleInfoMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly List<IOutputDeviceData> _outputData = [];

    public AzureDevOpsSummaryReporterTests()
    {
        _ = _configurationMock.SetupGet(c => c[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(ResultsDirectory);
        _ = _testApplicationModuleInfoMock.Setup(info => info.TryGetAssemblyName()).Returns("MyAssembly");
        _ = _loggerFactoryMock.Setup(loggerFactory => loggerFactory.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _ = _outputDeviceMock
            .Setup(outputDevice => outputDevice.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _outputData.Add(data))
            .Returns(Task.CompletedTask);
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsFalse_WhenSummaryOptionNotSetAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(options: []);
        Assert.IsFalse(await reporter.IsEnabledAsync().ConfigureAwait(false));
    }

    [TestMethod]
    public async Task SessionFinishing_DoesNothingAndEmitsWarning_WhenTfBuildNotSetAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns((string?)null);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.Contains(AzureDevOpsResources.SummaryRequiresTfBuildWarning, GetWarnings());

        await reporter.ConsumeAsync(CreateProducer(), CreatePassed("t1"), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        Assert.IsEmpty(GetFormattedLines());
    }

    [TestMethod]
    public void BuildMarkdown_RendersTotalsAndTopFailingAndSlowest()
    {
        var records = new List<AzureDevOpsSummaryReporter.TestRecord>
        {
            new("Test1", "MyCo.Suite.ClassA.Test1", AzureDevOpsSummaryReporter.TerminalKind.Passed, TimeSpan.FromMilliseconds(100)),
            new("Test2", "MyCo.Suite.ClassA.Test2", AzureDevOpsSummaryReporter.TerminalKind.Failed, TimeSpan.FromMilliseconds(200)),
            new("Test3", "MyCo.Suite.ClassA.Test3", AzureDevOpsSummaryReporter.TerminalKind.Failed, TimeSpan.FromMilliseconds(50)),
            new("Slowpoke", "MyCo.Suite.ClassB.Slowpoke", AzureDevOpsSummaryReporter.TerminalKind.Passed, TimeSpan.FromSeconds(12)),
            new("Skipper", "MyCo.Suite.ClassC.Skipper", AzureDevOpsSummaryReporter.TerminalKind.Skipped, TimeSpan.Zero),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("MyAssembly", md);
        Assert.Contains("net8.0", md);
        Assert.Contains("Total", md);
        Assert.Contains("Passed", md);
        Assert.Contains("Failed", md);
        Assert.Contains("Skipped", md);
        Assert.Contains("MyCo.Suite.ClassA", md);
        Assert.Contains("Slowpoke", md);
        Assert.Contains("MyCo.Suite.ClassA.Test2", md);
    }

    [TestMethod]
    public void BuildMarkdown_FormatsTotalDurationCorrectly_BeyondTwentyFourHours()
    {
        // 25 hours 7 minutes 8 seconds total: TimeSpan custom format `hh` wraps at 24h
        // (would render as 01:07:08), so the implementation must use TotalHours for >= 1h.
        var records = new List<AzureDevOpsSummaryReporter.TestRecord>
        {
            new("LongTest", "MyCo.X.LongTest", AzureDevOpsSummaryReporter.TerminalKind.Passed, new TimeSpan(25, 7, 8)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("| Total duration | 25:07:08 |", md);
        Assert.DoesNotContain("| Total duration | 01:07:08 |", md);
    }

    [TestMethod]
    public void BuildMarkdown_FormatsTotalDurationAsMinutesAndSeconds_WhenBelowOneHour()
    {
        var records = new List<AzureDevOpsSummaryReporter.TestRecord>
        {
            new("MidTest", "MyCo.X.MidTest", AzureDevOpsSummaryReporter.TerminalKind.Passed, new TimeSpan(0, 5, 30)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("| Total duration | 05:30 |", md);
    }

    [TestMethod]
    public void BuildMarkdown_EscapesPipesAndNewlinesInCells()
    {
        var records = new List<AzureDevOpsSummaryReporter.TestRecord>
        {
            new("Has|Pipe", "MyCo.X.HasPipe", AzureDevOpsSummaryReporter.TerminalKind.Failed, TimeSpan.FromMilliseconds(1)),
            new("Has\nNewline", "MyCo.X.HasNewline", AzureDevOpsSummaryReporter.TerminalKind.Failed, TimeSpan.FromMilliseconds(1)),
        };

        string md = AzureDevOpsSummaryReporter.BuildMarkdown(records, "MyAssembly", "net8.0");

        Assert.Contains("Has\\|Pipe", md);
        Assert.Contains("Has<br>Newline", md);
        Assert.DoesNotContain("Has|Pipe", md);
        Assert.DoesNotContain("Has\nNewline", md);
    }

    [TestMethod]
    public async Task SessionFinishing_WritesSummaryFileAndEmitsUploadSummaryCommandAsync()
    {
        AzureDevOpsSummaryReporter reporter = CreateReporter(EnabledOptions());
        _ = _environmentMock.Setup(e => e.GetEnvironmentVariable("TF_BUILD")).Returns("true");

        // Capture the StreamWriter output.
        using var memoryStream = new MemoryStream();
        IFileStream fakeStream = new FakeFileStream(memoryStream);
        _ = _fileSystemMock.Setup(fs => fs.ExistDirectory(It.IsAny<string>())).Returns(true);
        _ = _fileSystemMock.Setup(fs => fs.NewFileStream(It.IsAny<string>(), FileMode.Create, FileAccess.Write, FileShare.Read)).Returns(fakeStream);

        await reporter.OnTestSessionStartingAsync(new TestSessionContext()).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreatePassed("t1"), CancellationToken.None).ConfigureAwait(false);
        await reporter.ConsumeAsync(CreateProducer(), CreateFailed("t2"), CancellationToken.None).ConfigureAwait(false);
        await reporter.OnTestSessionFinishingAsync(new TestSessionContext()).ConfigureAwait(false);

        string written = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
        Assert.Contains("MyAssembly", written);

        string[] lines = GetFormattedLines();
        Assert.HasCount(1, lines);
        Assert.StartsWith("##vso[task.uploadsummary]", lines[0]);
        Assert.Contains("azdo-summary-", lines[0]);
        // The assembly name must be part of the default file name so concurrent test assemblies
        // sharing the same TFM and TestResults directory don't race to write the same file.
        Assert.Contains("MyAssembly", lines[0]);
        Assert.EndsWith(".md", lines[0]);
    }

    private static Dictionary<string, string[]> EnabledOptions()
        => new() { [AzureDevOpsCommandLineOptions.AzureDevOpsSummary] = [] };

    private AzureDevOpsSummaryReporter CreateReporter(Dictionary<string, string[]> options)
        => new(
            new TestCommandLineOptions(options),
            _configurationMock.Object,
            _environmentMock.Object,
            _fileSystemMock.Object,
            _outputDeviceMock.Object,
            _testApplicationModuleInfoMock.Object,
            _loggerFactoryMock.Object);

    private static TestNodeUpdateMessage CreatePassed(string uid)
        => Create(uid, new PassedTestNodeStateProperty());

    private static TestNodeUpdateMessage CreateFailed(string uid)
        => Create(uid, new FailedTestNodeStateProperty());

    private static TestNodeUpdateMessage Create(string uid, TestNodeStateProperty state)
        => new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = uid,
                DisplayName = uid,
                Properties = new PropertyBag(state),
            });

    private static IDataProducer CreateProducer() => new TestProducer();

    private string[] GetFormattedLines()
        => [.. _outputData.OfType<FormattedTextOutputDeviceData>().Select(output => output.Text)];

    private string[] GetWarnings()
        => [.. _outputData.OfType<WarningMessageOutputDeviceData>().Select(output => output.Message)];

    private sealed class TestProducer : IDataProducer
    {
        public Type[] DataTypesProduced { get; } = [typeof(TestNodeUpdateMessage)];

        public string Uid => "TestProducer";

        public string Version => "1.0.0";

        public string DisplayName => "TestProducer";

        public string Description => "TestProducer";

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

    private sealed class TestSessionContext : ITestSessionContext
    {
        public SessionUid SessionUid { get; } = new("session");

        public CancellationToken CancellationToken { get; } = CancellationToken.None;
    }

    private sealed class FakeFileStream(Stream inner) : IFileStream
    {
        public Stream Stream { get; } = inner;

        public string Name { get; } = "azdo-summary.md";

        public void Dispose()
        {
            // The underlying MemoryStream is owned by the test.
        }

        public ValueTask DisposeAsync() => default;
    }
}
