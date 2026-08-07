// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestApplicationResult : ITestApplicationProcessExitCode, IOutputDeviceDataProducer, IDisposable
{
    private readonly IOutputDevice _outputService;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IStopPoliciesService _policiesService;
    private readonly ITestCoverageResult? _testCoverageResult;
    private readonly OpenTelemetryResultHandler? _openTelemetryResultHandler;
    private readonly bool _isDiscovery;
    private int _failedTestsCount;
    private int _totalRanTests;
    private int _skippedTestsCount;
    private bool _testAdapterTestSessionFailure;

    public TestApplicationResult(
        IOutputDevice outputService,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IStopPoliciesService policiesService,
        IPlatformOpenTelemetryService? otelService)
        : this(outputService, commandLineOptions, environment, policiesService, otelService, testCoverageResult: null)
    {
    }

    public TestApplicationResult(
        IOutputDevice outputService,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IStopPoliciesService policiesService,
        IPlatformOpenTelemetryService? otelService,
        ITestCoverageResult? testCoverageResult)
    {
        _outputService = outputService;
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _policiesService = policiesService;
        _testCoverageResult = testCoverageResult;
        if (otelService is not null)
        {
            _openTelemetryResultHandler = new OpenTelemetryResultHandler(otelService, PlatformOpenTelemetryOptions.FromEnvironment(environment));
        }

        _isDiscovery = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
    }

    /// <inheritdoc />
    public string Uid => nameof(TestApplicationResult);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

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

        if (message.TestNode.Properties.Any<TestNodeExecutionCompletedProperty>())
        {
            _openTelemetryResultHandler?.NotifyExecutionCompleted(message.TestNode);
            return Task.CompletedTask;
        }

        if (executionState is null)
        {
            return Task.CompletedTask;
        }

        // A test framework that retries a test in-process (MSTest's [Retry], ...) reports every attempt under the
        // same test node uid. Only the final attempt is the test's outcome, so superseded attempts must not be
        // counted here: otherwise a test that failed once and then passed would still leave _failedTestsCount > 0
        // and make the process exit with ExitCode.AtLeastOneTestFailed.
        if (message.TestNode.IsSupersededRetryAttempt())
        {
            return Task.CompletedTask;
        }

        switch (executionState)
        {
            case DiscoveredTestNodeStateProperty:
                _openTelemetryResultHandler?.NotifyDiscovered();
                // In discovery mode, discovered tests count toward the "ran" total.
                if (_isDiscovery)
                {
                    _totalRanTests++;
                }

                break;

            case PassedTestNodeStateProperty passed:
                _openTelemetryResultHandler?.NotifyPassed(message.TestNode, passed);
                _totalRanTests++;
                break;

            case FailedTestNodeStateProperty:
            case ErrorTestNodeStateProperty:
            case TimeoutTestNodeStateProperty:
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            case CancelledTestNodeStateProperty:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                _openTelemetryResultHandler?.NotifyFailed(message.TestNode, executionState);
                _failedTestsCount++;
                _totalRanTests++;
                break;

            case SkippedTestNodeStateProperty skipped:
                _openTelemetryResultHandler?.NotifySkipped(message.TestNode, skipped);
                _skippedTestsCount++;
                // DESIGN: Skipped tests are intentionally excluded from `_totalRanTests`. Whether an all-skipped
                // run is treated as "ran nothing" depends on the `--zero-tests-policy` option resolved in
                // `GetProcessExitCode`:
                //   - `allow-skipped` (#9385, the default): skipped count as run, so only a run where nothing was
                //     discovered (`_totalRanTests == 0 && _skippedTestsCount == 0`) yields exit code
                //     `ExitCode.ZeroTests` (8). An all-skipped run succeeds.
                //   - `strict`: skipped don't count, so an all-skipped (or zero-test) run leaves
                //     `_totalRanTests == 0` and yields exit code 8. This is the original behavior from #3216 / #3243
                //     ("Skipped tests count as not run") which surfaced the common "invalid filter ran nothing" mistake.
                // The documented blunt opt-out for any of exit code 8 remains `--ignore-exit-code 8`.
                //
                // Two other layers mirror this decision and must stay in lockstep:
                //   - TerminalTestReporter.Summary.cs / TestRunSummaryHelper (`allTestsWereSkipped`)
                //   - Microsoft.Testing.Platform.MSBuild InvokeTestingPlatformTask (run-summary verdict)
                // Do not change this without revisiting those sites and the design discussion above.
                break;

            case InProgressTestNodeStateProperty:
                _openTelemetryResultHandler?.NotifyInProgress(message.TestNode, message.ParentTestNodeUid);
                break;

            default:
                _openTelemetryResultHandler?.NotifyUnknown();
                break;
        }

        return Task.CompletedTask;
    }

    public int GetProcessExitCode()
        => ExitCodeIgnorePolicy.Apply(GetProcessExitCodeWithoutIgnore(), _commandLineOptions, _environment);

    internal int GetProcessExitCodeWithoutIgnore()
    {
        ExitCode exitCode = ExitCode.Success;
        exitCode = exitCode == ExitCode.Success && _policiesService.IsMaxFailedTestsTriggered ? ExitCode.TestExecutionStoppedForMaxFailedTests : exitCode;
        exitCode = exitCode == ExitCode.Success && _testAdapterTestSessionFailure ? ExitCode.TestAdapterTestSessionFailure : exitCode;
        exitCode = exitCode == ExitCode.Success && _failedTestsCount > 0 ? ExitCode.AtLeastOneTestFailed : exitCode;
        exitCode = exitCode == ExitCode.Success && _policiesService.IsAbortTriggered ? ExitCode.TestSessionAborted : exitCode;

        // A deadline-driven graceful stop (see AbortAtDeadlineExtension) truncates the run before the CI
        // hard-cancel. Such a run may otherwise look successful (it did not fail or abort), but it did not
        // execute every test, so it must not report success. Real failures/abort above keep precedence; a
        // clean-but-truncated run becomes non-zero here and takes precedence over the zero-tests/coverage
        // verdicts below (a truncated run legitimately may not have run the expected number of tests).
        exitCode = exitCode == ExitCode.Success && _policiesService.IsDeadlineTriggered ? ExitCode.TestExecutionStoppedAtDeadline : exitCode;

        // An explicitly-provided `--minimum-expected-tests` governs the count-based verdict and
        // supersedes the ZeroTests (8) verdict below: a run of fewer than N tests yields
        // ExitCode.MinimumExpectedTestsPolicyViolation (9), even when zero tests ran. This lets callers
        // tell an explicit-minimum violation apart from a plain "ran nothing" run (e.g. so a
        // `dotnet test --test-modules` orchestrator can distinguish a stricter local minimum from an
        // empty module). See issue #7457.
        // A malformed value (e.g. present with no argument) is rejected earlier by option validation, but
        // we still guard the parse defensively and fall back to the zero-tests verdict if it ever slips through.
        if (_commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.MinimumExpectedTestsOptionKey, out string[]? argumentList)
            && argumentList is [string minimumExpectedTestsArgument]
            && int.TryParse(minimumExpectedTestsArgument, out int minimumExpectedTests))
        {
            exitCode = exitCode == ExitCode.Success && _totalRanTests < minimumExpectedTests ? ExitCode.MinimumExpectedTestsPolicyViolation : exitCode;
        }
        else
        {
            // Determine whether the run should be treated as having executed zero tests. Skipped tests are excluded
            // from `_totalRanTests`. Under the default `allow-skipped` policy (#9385) skipped tests count as run, so only
            // a run that discovered nothing at all counts as zero tests; under `strict` an all-skipped run also counts
            // as zero tests.
            ZeroTestsPolicy zeroTestsPolicy = PlatformCommandLineProvider.GetZeroTestsPolicy(_commandLineOptions);
            bool ranZeroTests = zeroTestsPolicy == ZeroTestsPolicy.AllowSkipped
                ? _totalRanTests == 0 && _skippedTestsCount == 0
                : _totalRanTests == 0;
            exitCode = exitCode == ExitCode.Success && ranZeroTests ? ExitCode.ZeroTests : exitCode;
        }

        // Coverage thresholds override only an otherwise-successful run. Count-based policies above retain
        // precedence when no tests ran or the explicit minimum was not met.
        exitCode = exitCode == ExitCode.Success && _testCoverageResult?.HasThresholdFailure == true ? ExitCode.CoverageThresholdFailed : exitCode;

        return (int)exitCode;
    }

    public async Task SetTestAdapterTestSessionFailureAsync(string errorMessage, CancellationToken cancellationToken)
    {
        TestAdapterTestSessionFailureErrorMessage = errorMessage;
        _testAdapterTestSessionFailure = true;
        await _outputService.DisplayAsync(this, new ErrorMessageOutputDeviceData(errorMessage), cancellationToken).ConfigureAwait(false);
    }

    public Statistics GetStatistics()
        => new() { TotalRanTests = _totalRanTests, TotalFailedTests = _failedTestsCount };

    /// <summary>
    /// Emits the run-level OpenTelemetry metrics and tags the root span with the run counts.
    /// </summary>
    /// <param name="runActivity">The root span of the run, if any.</param>
    /// <param name="exitCode">The exit code the host is about to return. Passed in rather than recomputed so the
    /// telemetry always agrees with what the process actually exits with, including on the cancellation path.</param>
    /// <remarks>
    /// This deliberately does not live in <see cref="Dispose"/>: services are disposed in registration order, and the
    /// OpenTelemetry provider is registered long before this one, so by the time we were disposed the
    /// <c>MeterProvider</c> had already been shut down and the measurement was silently dropped. The host calls this
    /// from its <c>finally</c> block instead, while the providers and the root span are still alive.
    /// </remarks>
    internal void ReportRunTelemetry(IPlatformActivity? runActivity, int exitCode)
        => _openTelemetryResultHandler?.NotifyRunCompleted(_totalRanTests, _failedTestsCount, _skippedTestsCount, exitCode, runActivity);

    public void Dispose()
        => _openTelemetryResultHandler?.Dispose();
}
