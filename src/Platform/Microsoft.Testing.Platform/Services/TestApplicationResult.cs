// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestApplicationResult : ITestApplicationProcessExitCode, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputService;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IStopPoliciesService _policiesService;
    private readonly IPlatformOpenTelemetryService? _otelService;
    private readonly ICounter<int>? _totalDiscoveredTests;
    private readonly ICounter<int>? _totalStartedTests;
    private readonly ICounter<int>? _totalCompletedTests;
    private readonly ICounter<int>? _totalPassedTests;
    private readonly ICounter<int>? _totalFailedTests;
    private readonly ICounter<int>? _totalSkippedTests;
    private readonly ICounter<int>? _totalUnknownedTests;
    private readonly IHistogram<double>? _totalDuration;
    private readonly bool _isDiscovery;
    private readonly Dictionary<TestNodeUid, IPlatformActivity?> _testActivities = [];
    private int _failedTestsCount;
    private int _totalRanTests;
    private bool _testAdapterTestSessionFailure;

    public TestApplicationResult(
        IOutputDevice outputService,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IStopPoliciesService policiesService,
        IPlatformOpenTelemetryService? otelService)
    {
        _outputService = outputService;
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _policiesService = policiesService;
        _otelService = otelService;
        _totalDiscoveredTests = otelService?.CreateCounter<int>("tests.discovered");
        _totalStartedTests = otelService?.CreateCounter<int>("tests.started");
        _totalCompletedTests = otelService?.CreateCounter<int>("tests.completed");
        _totalPassedTests = otelService?.CreateCounter<int>("tests.passed");
        _totalFailedTests = otelService?.CreateCounter<int>("tests.failed");
        _totalSkippedTests = otelService?.CreateCounter<int>("tests.skipped");
        _totalUnknownedTests = otelService?.CreateCounter<int>("tests.unknown");
        _totalDuration = otelService?.CreateHistogram<double>("tests.duration");
        _isDiscovery = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
    }

    /// <inheritdoc />
    public string Uid => nameof(TestApplicationResult);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = PlatformResources.TestApplicationResultDisplayName;

    /// <inheritdoc />
    public string Description { get; } = PlatformResources.TestApplicationResultDescription;

    /// <inheritdoc />
    public Type[] DataTypesConsumed { get; }
        = [typeof(TestNodeUpdateMessage)];

    public bool HasTestAdapterTestSessionFailure => TestAdapterTestSessionFailureErrorMessage is not null;

    public string? TestAdapterTestSessionFailureErrorMessage { get; private set; }

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        var message = (TestNodeUpdateMessage)value;
        TestNodeStateProperty? executionState = message.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();

        if (executionState is null)
        {
            return Task.CompletedTask;
        }

        switch (executionState)
        {
            case DiscoveredTestNodeStateProperty:
                _totalDiscoveredTests?.Add(1);
                break;

            case PassedTestNodeStateProperty passed:
                _totalPassedTests?.Add(1);
                _totalCompletedTests?.Add(1);
                HandleTestResult(message.TestNode, passed);
                break;

            case FailedTestNodeStateProperty:
            case ErrorTestNodeStateProperty:
            case TimeoutTestNodeStateProperty:
            case CancelledTestNodeStateProperty:
                _totalFailedTests?.Add(1);
                _totalCompletedTests?.Add(1);
                HandleTestResult(message.TestNode, executionState);
                break;

            case SkippedTestNodeStateProperty skipped:
                _totalSkippedTests?.Add(1);
                _totalCompletedTests?.Add(1);
                HandleTestResult(message.TestNode, skipped);
                break;

            case InProgressTestNodeStateProperty:
                _totalStartedTests?.Add(1);
                _testActivities.Add(
                    message.TestNode.Uid,
                    _otelService?.StartActivity(
                        message.TestNode.Uid,
                        parentId: _otelService?.TestFrameworkActivity?.Id,
                        tags: GetTestInitialInfo(message.TestNode, message.ParentTestNodeUid)));
                break;

            default:
                _totalUnknownedTests?.Add(1);
                break;
        }

        Type outcomeType = executionState.GetType();
        if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, outcomeType) != -1)
        {
            _failedTestsCount++;
        }

        if (_isDiscovery
            && Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeDiscoveredProperties, outcomeType) != -1)
        {
            _totalRanTests++;
        }
        else if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeProperties, outcomeType) != -1)
        {
            _totalRanTests++;
        }

        return Task.CompletedTask;
    }

    public int GetProcessExitCode()
    {
        int exitCode = ExitCodes.Success;
        exitCode = exitCode == ExitCodes.Success && _policiesService.IsMaxFailedTestsTriggered ? ExitCodes.TestExecutionStoppedForMaxFailedTests : exitCode;
        exitCode = exitCode == ExitCodes.Success && _testAdapterTestSessionFailure ? ExitCodes.TestAdapterTestSessionFailure : exitCode;
        exitCode = exitCode == ExitCodes.Success && _failedTestsCount > 0 ? ExitCodes.AtLeastOneTestFailed : exitCode;
        exitCode = exitCode == ExitCodes.Success && _policiesService.IsAbortTriggered ? ExitCodes.TestSessionAborted : exitCode;
        exitCode = exitCode == ExitCodes.Success && _totalRanTests == 0 ? ExitCodes.ZeroTests : exitCode;

        if (_commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.MinimumExpectedTestsOptionKey, out string[]? argumentList))
        {
            exitCode = exitCode == ExitCodes.Success && _totalRanTests < int.Parse(argumentList[0], CultureInfo.InvariantCulture) ? ExitCodes.MinimumExpectedTestsPolicyViolation : exitCode;
        }

        // If the user has specified the IgnoreExitCode, then we don't want to return a non-zero exit code if the exit code matches the one specified.
        string? exitCodeToIgnore = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_EXITCODE_IGNORE);
        if (RoslynString.IsNullOrEmpty(exitCodeToIgnore))
        {
            if (_commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.IgnoreExitCodeOptionKey, out string[]? commandLineExitCodes) && commandLineExitCodes.Length > 0)
            {
                exitCodeToIgnore = commandLineExitCodes[0];
            }
        }

        if (exitCodeToIgnore is not null)
        {
            if (exitCodeToIgnore.Split(';').Any(code => int.TryParse(code, out int parsedExitCode) && parsedExitCode == exitCode))
            {
                exitCode = ExitCodes.Success;
            }
        }

        return exitCode;
    }

    public async Task SetTestAdapterTestSessionFailureAsync(string errorMessage, CancellationToken cancellationToken)
    {
        TestAdapterTestSessionFailureErrorMessage = errorMessage;
        _testAdapterTestSessionFailure = true;
        await _outputService.DisplayAsync(this, new ErrorMessageOutputDeviceData(errorMessage), cancellationToken).ConfigureAwait(false);
    }

    public Statistics GetStatistics()
        => new() { TotalRanTests = _totalRanTests, TotalFailedTests = _failedTestsCount };

    private static IEnumerable<KeyValuePair<string, object?>> GetTestInitialInfo(TestNode testNode, TestNodeUid? parentUid)
    {
        yield return new("test.name", testNode.DisplayName);
        yield return new("test.id", testNode.Uid.Value);
        if (parentUid is not null)
        {
            yield return new("test.parent.id", parentUid.Value);
        }

        if (testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>() is { } identifierProperty)
        {
            yield return new("test.method", identifierProperty.MethodName);
            yield return new("test.class", identifierProperty.TypeName);
            yield return new("test.namespace", identifierProperty.Namespace);
            yield return new("test.assembly", identifierProperty.AssemblyFullName);
        }

        if (testNode.Properties.SingleOrDefault<TestFileLocationProperty>() is { } testLocationProperty)
        {
            yield return new("test.file.path", testLocationProperty.FilePath);
            yield return new("test.line.start", testLocationProperty.LineSpan.Start.Line);
            yield return new("test.line.end", testLocationProperty.LineSpan.End.Line);
        }

        foreach (TestMetadataProperty metadata in testNode.Properties.OfType<TestMetadataProperty>())
        {
            yield return new KeyValuePair<string, object?>($"test.metadataProperty.{metadata.Key}", metadata.Value);
        }
    }

    private void HandleTestResult(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        if (!_testActivities.TryGetValue(testNode.Uid, out IPlatformActivity? activity))
        {
            return;
        }

        (string result, Exception? exception, TimeSpan? timeoutTime) = stateProperty switch
        {
            PassedTestNodeStateProperty => ("passed", null, null),
            FailedTestNodeStateProperty failed => ("failed", failed.Exception, null),
            ErrorTestNodeStateProperty error => ("error", error.Exception, null),
            TimeoutTestNodeStateProperty timeout => ("timeout", timeout.Exception, timeout.Timeout),
            CancelledTestNodeStateProperty cancelled => ("cancelled", cancelled.Exception, null),
            SkippedTestNodeStateProperty => ("skipped", null, null),
            _ => ("unknown", null, null),
        };

        activity?.SetTag("test.result", result);
        activity?.SetTag("test.result.explanation", stateProperty.Explanation);
        if (exception is not null)
        {
            activity?.SetTag("test.result.exception.type", exception.GetType().FullName);
            activity?.SetTag("test.result.exception.message", exception.Message);
            activity?.SetTag("test.result.exception.stacktrace", exception.StackTrace);
        }

        if (timeoutTime is not null)
        {
            activity?.SetTag("test.result.timeout.ms", timeoutTime.Value.TotalMilliseconds);
        }

        if (testNode.Properties.SingleOrDefault<TimingProperty>() is { } timingProperty)
        {
            double totalMilliseconds = timingProperty.GlobalTiming.Duration.TotalMilliseconds;
            _totalDuration?.Record(totalMilliseconds);
            activity?.SetTag("test.duration.ms", totalMilliseconds);
            foreach (StepTimingInfo step in timingProperty.StepTimings)
            {
                activity?.SetTag($"test.step{step.Id}.duration.ms", step.Timing.Duration.TotalMilliseconds);
                activity?.SetTag($"test.step{step.Id}.description", step.Description);
            }
        }

        foreach (TestMetadataProperty metadataProperty in testNode.Properties.OfType<TestMetadataProperty>())
        {
            activity?.SetTag($"test.metadataProperty.{metadataProperty.Key}", metadataProperty.Value);
        }

        activity?.SetTag("test.stdout", string.Join(_environment.NewLine, testNode.Properties.OfType<StandardOutputProperty>().Select(x => x.StandardOutput)));
        activity?.SetTag("test.stderr", string.Join(_environment.NewLine, testNode.Properties.OfType<StandardErrorProperty>().Select(x => x.StandardError)));

        int index = 0;
        foreach (FileArtifactProperty fileArtifactProperty in testNode.Properties.OfType<FileArtifactProperty>())
        {
            activity?.SetTag($"test.artifact.file[{index}].path", fileArtifactProperty.FileInfo.FullName);
            index++;
        }

        activity?.Dispose();
        _testActivities.Remove(testNode.Uid);
    }
}
