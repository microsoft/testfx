// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestApplicationResult : ITestApplicationProcessExitCode, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputService;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IStopPoliciesService _policiesService;
    private readonly bool _isDiscovery;
    private int _failedTestsCount;
    private int _totalRanTests;
    private bool _testAdapterTestSessionFailure;

    public TestApplicationResult(
        IOutputDevice outputService,
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IStopPoliciesService policiesService)
    {
        _outputService = outputService;
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _policiesService = policiesService;
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

        if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, executionState.GetType()) != -1)
        {
            _failedTestsCount++;
        }

        if (_isDiscovery
            && Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeDiscoveredProperties, executionState.GetType()) != -1)
        {
            _totalRanTests++;
        }
        else if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeProperties, executionState.GetType()) != -1)
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
}
