// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

extern alias ghactions;

using System.Reflection;

using Microsoft.Testing.Extensions.AzureDevOpsReport;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.UnitTests.Helpers;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

using GitHubActionsResources = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.Resources.GitHubActionsResources;
using GitHubActionsSummaryArtifactPostProcessor = ghactions::Microsoft.Testing.Extensions.GitHubActionsReport.GitHubActionsSummaryArtifactPostProcessor;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class CiCoverageSummaryControllerHandlerRegistrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ci-coverage-registration-{Guid.NewGuid():N}");
    private readonly ServiceProvider _services = new();
    private readonly Mock<IEnvironment> _environment = new();
    private readonly Mock<ITestCoverageResult> _coverage = new();
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<ILoggerFactory> _loggerFactory = new();
    private readonly Mock<IOutputDevice> _outputDevice = new();
    private readonly List<IOutputDeviceData> _output = [];

    public CiCoverageSummaryControllerHandlerRegistrationTests()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.SetupGet(item => item[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(_directory);
        _coverage.SetupGet(item => item.Scopes).Returns([]);
        _coverage.SetupGet(item => item.Thresholds).Returns(
        [
            new TestCoverageThresholdMessage(
                new SessionUid("session"),
                CoverageScope.Overall,
                CoverageMetric.Line,
                CoverageAggregation.None,
                actualPercentage: 75,
                requiredPercentage: 80,
                hasCoverableData: true,
                producerId: nameof(CoverageThresholdPolicy)),
        ]);
        _logger.Setup(item => item.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _logger.Setup(item => item.LogAsync(
            It.IsAny<LogLevel>(),
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<string, Exception?, string>>())).Returns(Task.CompletedTask);
        _loggerFactory.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(_logger.Object);
        _outputDevice.Setup(item => item.DisplayAsync(
            It.IsAny<IOutputDeviceDataProducer>(),
            It.IsAny<IOutputDeviceData>(),
            It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) => _output.Add(data))
            .Returns(Task.CompletedTask);
        _services.AddServices(
        [
            configuration.Object,
            _environment.Object,
            _coverage.Object,
            _loggerFactory.Object,
            _outputDevice.Object,
        ]);
    }

    [TestMethod]
    [DataRow(false, null, false)]
    [DataRow(true, null, false)]
    [DataRow(false, "coverage-threshold-line", true)]
    [DataRow(true, "coverage-threshold-line", true)]
    [DataRow(false, "coverage-threshold-branch", true)]
    [DataRow(true, "coverage-threshold-branch", true)]
    public async Task AddProvider_CreatesHandlerWithProviderIdentityAndThresholdEnablement(
        bool gitHubActions, string? thresholdOption, bool enabled)
    {
        Dictionary<string, string[]> options = [];
        if (thresholdOption is not null)
        {
            options[thresholdOption] = ["80"];
        }

        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions, options);

        Assert.AreEqual($"{GetProvider(gitHubActions)}.CoverageSummaryFinalizer", handler.Uid);
        Assert.AreEqual(GetProvider(gitHubActions), handler.DisplayName);
        Assert.AreEqual(enabled, await handler.IsEnabledAsync());
        string loggerCategory = gitHubActions
            ? typeof(GitHubActionsSummaryArtifactPostProcessor).FullName!
            : typeof(AzureDevOpsSummaryArtifactPostProcessor).FullName!;
        _loggerFactory.Verify(item => item.CreateLogger(loggerCategory), Times.Once);
    }

    [TestMethod]
    [DataRow(false, null, true)]
    [DataRow(true, null, true)]
    [DataRow(false, "internal-retry-pipename", true)]
    [DataRow(true, "internal-retry-pipename", false)]
    [DataRow(false, "internal-testhostcontroller-pid", true)]
    [DataRow(true, "internal-testhostcontroller-pid", false)]
    public async Task AddProvider_FinalizesMatchingFragmentAndPreservesProviderDeferral(
        bool gitHubActions, string? deferOption, bool publishesSummary)
    {
        Dictionary<string, string[]> options = new() { ["coverage-threshold-line"] = ["80"] };
        if (deferOption is not null)
        {
            options[deferOption] = ["123"];
        }

        _environment.Setup(item => item.GetEnvironmentVariable("TESTINGPLATFORM_TESTHOSTCONTROLLER_COVERAGEPOLICY_123")).Returns("1");
        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions, options);
        SessionFileArtifact artifact = await CreateFragmentAsync(gitHubActions);
        // Processors are registered after factory invocation to protect lazy resolution.
        if (publishesSummary)
        {
            var commandLine = new TestCommandLineOptions(options);
            _services.AddServices(
            [
                new AzureDevOpsSummaryArtifactPostProcessor(commandLine, _environment.Object, _outputDevice.Object),
                new GitHubActionsSummaryArtifactPostProcessor(
                    commandLine, _environment.Object, new SystemFileSystem(), _loggerFactory.Object, static () => false),
            ]);
        }

        await handler.OnRunCompletedAsync(2, [artifact], CancellationToken.None);

        var input = new InputArtifact(artifact.FileInfo.FullName, artifact.Kind, null, null, null, null);
        CiRunSummaryModule module = Assert.ContainsSingle(CiRunSummaryAggregation.ReadAndAggregate(
            [input], GetProvider(gitHubActions), new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None)).Modules);
        Assert.AreEqual(2, module.ExitCode);
        CiCoverageThreshold threshold = Assert.ContainsSingle(module.Coverage.Thresholds);
        Assert.AreEqual(nameof(CoverageThresholdPolicy), threshold.ProducerId);
        Assert.AreEqual(CoverageMetric.Line, threshold.Metric);
        Assert.AreEqual(75, threshold.ActualPercentage);
        Assert.AreEqual(80, threshold.RequiredPercentage);
        string mergedDirectory = Path.Combine(_directory, "merged");
        Assert.AreEqual(publishesSummary, Directory.Exists(mergedDirectory));
        if (publishesSummary)
        {
            string summary = Assert.ContainsSingle(Directory.GetFiles(mergedDirectory, "*.md"));
            Assert.StartsWith($"{GetProvider(gitHubActions)}-summary-", Path.GetFileName(summary));
            Assert.Contains("Coverage thresholds", File.ReadAllText(summary));
        }

        Assert.IsEmpty(_output.OfType<WarningMessageOutputDeviceData>());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddProvider_IgnoresOtherProviderFragments(bool gitHubActions)
    {
        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions);
        SessionFileArtifact artifact = await CreateFragmentAsync(!gitHubActions);
        string original = File.ReadAllText(artifact.FileInfo.FullName);

        await handler.OnRunCompletedAsync(2, [artifact], CancellationToken.None);

        Assert.AreEqual(original, File.ReadAllText(artifact.FileInfo.FullName));
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "merged")));
        Assert.IsEmpty(_output);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddProvider_WithoutControllerThresholds_DoesNotFinalize(bool gitHubActions)
    {
        _coverage.SetupGet(item => item.Thresholds).Returns([]);
        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions);
        SessionFileArtifact artifact = await CreateFragmentAsync(gitHubActions);
        string original = File.ReadAllText(artifact.FileInfo.FullName);

        await handler.OnRunCompletedAsync(2, [artifact], CancellationToken.None);

        Assert.AreEqual(original, File.ReadAllText(artifact.FileInfo.FullName));
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "merged")));
        Assert.IsEmpty(_output);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddProvider_InvalidFragment_UsesProviderWarningAndLogger(bool gitHubActions)
    {
        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions);
        SessionFileArtifact artifact = await CreateFragmentAsync(gitHubActions);
        File.WriteAllText(artifact.FileInfo.FullName, "{}");

        await handler.OnRunCompletedAsync(2, [artifact], CancellationToken.None);

        string error = $"Invalid {GetProvider(gitHubActions)} summary fragment '{artifact.FileInfo.FullName}'.";
        string expected = string.Format(
            CultureInfo.InvariantCulture,
            gitHubActions ? GitHubActionsResources.StepSummaryWriteFailedWarning : AzureDevOpsResources.SummaryWriteFailedWarning,
            _directory,
            error);
        WarningMessageOutputDeviceData warning = Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(Assert.ContainsSingle(_output));
        Assert.AreEqual(expected, warning.Message);
        _logger.Verify(
            item => item.LogAsync(LogLevel.Warning, expected, null, It.IsAny<Func<string, Exception?, string>>()),
            Times.Once);
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "merged")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AddProvider_Cancellation_DoesNotFinalizeOrEmitWarning(bool gitHubActions)
    {
        ITestHostControllerRunCompletionHandler handler = await CreateHandlerAsync(gitHubActions);
        SessionFileArtifact artifact = await CreateFragmentAsync(gitHubActions);
        string original = File.ReadAllText(artifact.FileInfo.FullName);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => handler.OnRunCompletedAsync(2, [artifact], cancellation.Token));

        Assert.AreEqual(original, File.ReadAllText(artifact.FileInfo.FullName));
        Assert.IsFalse(Directory.Exists(Path.Combine(_directory, "merged")));
        Assert.IsEmpty(_output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<ITestHostControllerRunCompletionHandler> CreateHandlerAsync(
        bool gitHubActions, Dictionary<string, string[]>? options = null)
    {
        _services.AddService(new TestCommandLineOptions(options ?? new() { ["coverage-threshold-line"] = ["80"] }));
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync([]);
        if (gitHubActions)
        {
            ghactions::Microsoft.Testing.Extensions.GitHubActionsExtensions.AddGitHubActionsProvider(builder);
        }
        else
        {
            builder.AddAzureDevOpsProvider();
        }

        return Assert.ContainsSingle(GetRunCompletionHandlerFactories(builder))(_services);
    }

    private async Task<SessionFileArtifact> CreateFragmentAsync(bool gitHubActions)
    {
        string provider = GetProvider(gitHubActions);
        string path = await CiRunSummaryAggregation.WriteFragmentAsync(
            _directory,
            provider,
            provider,
            new CiRunSummaryModule
            {
                AssemblyName = "Tests",
                ModulePath = Path.Combine(_directory, "Tests.dll"),
                TargetFramework = "net8.0",
                Architecture = "x64",
                SessionUid = "session",
                AttemptNumber = 1,
                TotalTests = 1,
                PassedTests = 1,
            });
        string kind = gitHubActions
            ? "microsoft.testing.github-actions-summary-fragment"
            : "microsoft.testing.azure-devops-summary-fragment";
        return new SessionFileArtifact(new SessionUid("session"), new FileInfo(path), "Summary", description: null, kind);
    }

    private static string GetProvider(bool gitHubActions) => gitHubActions ? "github-actions" : "azure-devops";

    private static List<Func<IServiceProvider, ITestHostControllerRunCompletionHandler>> GetRunCompletionHandlerFactories(ITestApplicationBuilder builder)
    {
        FieldInfo factoriesField = typeof(TestHostControllersManager).GetField(
            "_runCompletionHandlerFactories",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<Func<IServiceProvider, ITestHostControllerRunCompletionHandler>>)factoriesField.GetValue(builder.TestHostControllers)!;
    }
}
