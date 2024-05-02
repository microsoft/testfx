﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Services;

internal sealed class TestApplicationResult(
    IOutputDevice outputService,
    ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
    ICommandLineOptions commandLineOptions,
    IEnvironment environment) : ITestApplicationProcessExitCode, ILoggerProvider, IOutputDeviceDataProducer
{
    private readonly IOutputDevice _outputService = outputService;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    private readonly ICommandLineOptions _commandLineOptions = commandLineOptions;
    private readonly IEnvironment _environment = environment;
    private readonly List<TestApplicationResultLogger> _testApplicationResultLoggers = [];
    private readonly List<TestNode> _failedTests = [];
    private int _totalRanTests;
    private bool _testAdapterTestSessionFailure;

    /// <inheritdoc />
    public string Uid { get; } = nameof(TestApplicationResult);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

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
            _failedTests.Add(message.TestNode);
        }

        if (_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
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

    public ILogger CreateLogger(string categoryName)
    {
        TestApplicationResultLogger logger = new(categoryName);
        _testApplicationResultLoggers.Add(logger);

        return logger;
    }

    public async Task<int> GetProcessExitCodeAsync()
    {
        bool anyError = false;
        foreach ((string categoryName, string error) in _testApplicationResultLoggers.SelectMany(logger => logger.Errors.Select(error => (logger.CategoryName, error))))
        {
            anyError = true;
            await _outputService.DisplayAsync(this, FormattedTextOutputDeviceDataBuilder.CreateRedConsoleColorText($"[{categoryName}] {error}"));
        }

        int exitCode = ExitCodes.Success;
        exitCode = anyError ? ExitCodes.GenericFailure : exitCode;
        exitCode = exitCode == ExitCodes.Success && _testAdapterTestSessionFailure ? ExitCodes.TestAdapterTestSessionFailure : exitCode;
        exitCode = exitCode == ExitCodes.Success && _failedTests.Count > 0 ? ExitCodes.AtLeastOneTestFailed : exitCode;
        exitCode = exitCode == ExitCodes.Success && _testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested ? ExitCodes.TestSessionAborted : exitCode;

        // If the user has specified the VSTestAdapterMode option, then we don't want to return a non-zero exit code if no tests ran.
        if (!_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey))
        {
            exitCode = exitCode == ExitCodes.Success && _totalRanTests == 0 ? ExitCodes.ZeroTests : exitCode;
        }

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

    public async Task SetTestAdapterTestSessionFailureAsync(string errorMessage)
    {
        TestAdapterTestSessionFailureErrorMessage = errorMessage;
        _testAdapterTestSessionFailure = true;
        await _outputService.DisplayAsync(this, FormattedTextOutputDeviceDataBuilder.CreateRedConsoleColorText(errorMessage));
    }

    public Statistics GetStatistics()
        => new() { TotalRanTests = _totalRanTests, TotalFailedTests = _failedTests.Count };

    private sealed class TestApplicationResultLogger(string categoryName) : ILogger
    {
        private readonly ConcurrentBag<string> _errors = [];

        public IReadOnlyCollection<string> Errors => _errors;

        public string CategoryName { get; } = categoryName;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel is LogLevel.Error or LogLevel.Critical;

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _errors.Add(formatter(state, exception));
            return Task.CompletedTask;
        }

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _errors.Add(formatter(state, exception));
    }
}
