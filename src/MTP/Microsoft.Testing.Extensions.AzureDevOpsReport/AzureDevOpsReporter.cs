// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsReporter :
    IDataConsumer,
    IOutputDeviceDataProducer
{
    internal const double KnownFlakyFailureRateThreshold = 0.25;
    private const int MinSamplesForRegressionAnnotation = 5;
    private const string QuarantineBuildTagLine = "##vso[build.addbuildtag]has-quarantined-test-failure";
    private const string WarningSeverity = "warning";

    private readonly IOutputDevice _outputDisplay;
    private readonly ILogger _logger;
    private readonly ICommandLineOptions _commandLine;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IAzureDevOpsHistoryService _historyService;
    private readonly string? _contextualAssemblyIdentity;
    private readonly string _targetFrameworkMoniker;
    private string? _severity;
    private bool _demoteKnownFlaky;
    private QuarantineFile? _quarantineFile;
    private bool _hasLoadedEnabledConfiguration;
    private int _quarantineBuildTagEmitted;
    private Regex[]? _userStackFrameFilters;

    public AzureDevOpsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory,
        IAzureDevOpsHistoryService historyService)
        : this(commandLine, environment, fileSystem, outputDisplay, loggerFactory, historyService, testApplicationModuleInfo: null)
    {
    }

    public AzureDevOpsReporter(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDisplay,
        ILoggerFactory loggerFactory,
        IAzureDevOpsHistoryService historyService,
        ITestApplicationModuleInfo? testApplicationModuleInfo)
    {
        _commandLine = commandLine;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDisplay = outputDisplay;
        _historyService = historyService;
        _contextualAssemblyIdentity = string.Equals(
            environment.GetEnvironmentVariable(AzureDevOpsLogGroupReporter.DisableHostGroupsEnvironmentVariable),
            "1",
            StringComparison.Ordinal)
                ? testApplicationModuleInfo?.GetDisplayName()
                : null;
        _logger = loggerFactory.CreateLogger<AzureDevOpsReporter>();
        _targetFrameworkMoniker = TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform();
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
    ];

    /// <inheritdoc />
    public string Uid => nameof(AzureDevOpsReporter);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = AzureDevOpsResources.DisplayName;

    /// <inheritdoc />
    public string Description { get; } = AzureDevOpsResources.Description;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync()
    {
        bool isEnabledByParameter = _commandLine.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName);
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync($"{nameof(AzureDevOpsReport)} is {(isEnabledByParameter ? "enabled" : "disabled")}.").ConfigureAwait(false);
        }

        if (!isEnabledByParameter)
        {
            return false;
        }

        bool isEnabledByEnvVariable = AzureDevOpsConstants.IsRunningInAzureDevOps(_environment);
        if (isEnabledByEnvVariable)
        {
            EnsureEnabledConfigurationLoaded();
        }

        bool annotationsEnabled = AzureDevOpsConstants.IsFeatureKnobEnabled(_commandLine, AzureDevOpsCommandLineOptions.AzureDevOpsAnnotations);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync($"{AzureDevOpsConstants.TfBuildEnvironmentVariableName} environment variable is {(isEnabledByEnvVariable ? "enabled. Will report errors to Azure DevOps, because we are running in CI." : "disabled. Will not report errors to Azure DevOps.")}").ConfigureAwait(false);
            await _logger.LogTraceAsync($"Severity is set to '{_severity ?? "error"}', you can override it by using --report-azdo-severity parameter.").ConfigureAwait(false);
            await _logger.LogTraceAsync($"Failure annotations are {(annotationsEnabled ? "enabled" : "disabled")}, you can toggle them by using --report-azdo-annotations parameter.").ConfigureAwait(false);
        }

        return isEnabledByEnvVariable && annotationsEnabled;
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is not TestNodeUpdateMessage nodeUpdateMessage)
        {
            return;
        }

        EnsureEnabledConfigurationLoaded();

        // A test framework that retries a test in-process reports every attempt under the same test node uid. An
        // attempt that a later one supersedes is not a build failure, so it must not raise an Azure DevOps error
        // annotation: only the final attempt is annotated.
        if (nodeUpdateMessage.TestNode.IsSupersededRetryAttempt())
        {
            return;
        }

        TestNodeStateProperty? nodeState = nodeUpdateMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        string testDisplayName = nodeUpdateMessage.TestNode.DisplayName;

        // Defer GetTestName() to failure branches only: for passing/skipped/in-progress tests
        // nodeState falls through the switch with no match and testName is never needed.
        switch (nodeState)
        {
            case FailedTestNodeStateProperty failed:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), failed.Explanation, failed.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case ErrorTestNodeStateProperty error:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), error.Explanation, error.Exception, cancellationToken).ConfigureAwait(false);
                break;
            case TimeoutTestNodeStateProperty timeout:
                await WriteExceptionAsync(testDisplayName, GetTestName(nodeUpdateMessage.TestNode), timeout.Explanation, timeout.Exception, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static string GetTestName(TestNode testNode)
        => TestNodeIdentity.GetTestName(testNode);
}
