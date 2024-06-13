// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.OutputDevice;

internal class ConsoleOutputDevice : IPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly List<SessionFileArtifact> _sessionFilesArtifact = [];
    private readonly List<FileArtifact> _filesArtifact = [];
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly IConsole _console;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IProcessHandler _process;
    private readonly IPlatformInformation _platformInformation;
    private readonly bool _isVSTestMode;
    private readonly bool _isListTests;
    private readonly bool _isServerMode;
    private readonly int _minimumExpectedTest;
    private readonly ILogger? _logger;
    private readonly FileLoggerProvider? _fileLoggerProvider;
    private readonly bool _underProcessMonitor;

    private int _totalTests;
    private int _totalPassedTests;
    private int _totalFailedTests;
    private int _totalSkippedTests;
    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _bannerDisplayed;
    private TestRequestExecutionTimeInfo? _testRequestExecutionTimeInfo;

    public ConsoleOutputDevice(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource, IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, ITestHostControllerInfo testHostControllerInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler process, IPlatformInformation platformInformation,
        bool isVSTestMode, bool isListTests, bool isServerMode, int minimumExpectedTest, FileLoggerProvider? fileLoggerProvider)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _console = console;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _asyncMonitor = asyncMonitor;
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _process = process;
        _platformInformation = platformInformation;
        _isVSTestMode = isVSTestMode;
        _isListTests = isListTests;
        _isServerMode = isServerMode;
        _minimumExpectedTest = minimumExpectedTest;
        _fileLoggerProvider = fileLoggerProvider;
        if (_fileLoggerProvider is not null)
        {
            _logger = _fileLoggerProvider.CreateLogger(GetType().ToString());
        }

        if (testHostControllerInfo.HasTestHostController)
        {
            if (environment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{testHostControllerInfo.GetTestHostControllerPID(true)}") is not null)
            {
                _underProcessMonitor = true;
            }
        }

        if (environment.GetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }

        _testApplicationCancellationTokenSource.CancellationToken.Register(() => _console.WriteLine(PlatformResources.CancellingTestSession));
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
        typeof(TestRequestExecutionTimeInfo),
    ];

    /// <inheritdoc />
    public virtual string Uid { get; } = nameof(ConsoleOutputDevice);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "Test Platform Console Service";

    /// <inheritdoc />
    public string Description { get; } = "Test Platform default console service";

    /// <inheritdoc />
    public virtual Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogDebugAsync(string message)
    {
        if (_logger is not null)
        {
            await _logger.LogDebugAsync(message);
        }
    }

    public virtual async Task DisplayBannerAsync(string? bannerMessage)
    {
        if (_isVSTestMode)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            if (!_bannerDisplayed)
            {
                // skip the banner for the children processes
                _environment.SetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

                _bannerDisplayed = true;

                if (bannerMessage is not null)
                {
                    _console.WriteLine(bannerMessage);
                }
                else
                {
                    StringBuilder stringBuilder = new();
                    stringBuilder.Append(_platformInformation.Name);

                    if (_platformInformation.Version is { } version)
                    {
                        stringBuilder.Append(CultureInfo.InvariantCulture, $" v{version}");
                        if (_platformInformation.CommitHash is { } commitHash)
                        {
                            stringBuilder.Append(CultureInfo.InvariantCulture, $"+{commitHash[..10]}");
                        }
                    }

                    if (_platformInformation.BuildDate is { } buildDate)
                    {
                        stringBuilder.Append(CultureInfo.InvariantCulture, $" (UTC {buildDate.UtcDateTime})");
                    }

                    if (_runtimeFeature.IsDynamicCodeSupported)
                    {
                        stringBuilder.Append(" [");
#if !NETCOREAPP
                        stringBuilder.Append(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
#else
                        stringBuilder.Append(RuntimeInformation.RuntimeIdentifier);
#endif
                        stringBuilder.Append(CultureInfo.InvariantCulture, $" - {RuntimeInformation.FrameworkDescription}]");
                    }

                    _console.WriteLine(stringBuilder.ToString());
                }
            }

            if (_fileLoggerProvider is not null)
            {
                ConsoleColor currentForegroundColor = _console.GetForegroundColor();
                try
                {
                    _console.SetForegroundColor(ConsoleColor.Yellow);
                    if (_fileLoggerProvider.SyncFlush)
                    {
                        _console.WriteLine(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerProvider.LogLevel, _fileLoggerProvider.FileLogger.FileName));
                    }
                    else
                    {
                        _console.WriteLine(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerProvider.LogLevel, _fileLoggerProvider.FileLogger.FileName));
                    }
                }
                finally
                {
                    _console.SetForegroundColor(currentForegroundColor);
                }
            }
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("DisplayBeforeSessionStartAsync");
        }
    }

    public async Task DisplayAfterSessionEndRunAsync()
    {
        if (_isVSTestMode || _isListTests || _isServerMode)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            string? runtimeInformation = null;
            if (_runtimeFeature.IsDynamicCodeSupported)
            {
#if !NETCOREAPP
                runtimeInformation = $"{RuntimeInformation.FrameworkDescription}";
#else
                runtimeInformation = $"{RuntimeInformation.RuntimeIdentifier} - {RuntimeInformation.FrameworkDescription}";
#endif
            }

            string? moduleName = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();
#if !NETCOREAPP
            moduleName = RoslynString.IsNullOrEmpty(moduleName)
                ? _process.GetCurrentProcess().MainModule.FileName
                : moduleName;
#else
            moduleName = RoslynString.IsNullOrEmpty(moduleName)
                ? _environment.ProcessPath
                : moduleName;
#endif
            string moduleOutput = moduleName is not null
                ? $" for {moduleName}"
                : string.Empty;

            if (!_firstCallTo_OnSessionStartingAsync)
            {
                int totalTests = Thread.VolatileRead(ref _totalTests);
                int failedTests = Thread.VolatileRead(ref _totalFailedTests);
                int passedTests = Thread.VolatileRead(ref _totalPassedTests);
                int skippedTests = Thread.VolatileRead(ref _totalSkippedTests);

                bool runFailed = failedTests > 0;
                string passedOrFailedOrAborted = string.Format(CultureInfo.CurrentCulture, "{0}!", runFailed ? PlatformResources.Failed : PlatformResources.Passed);
                passedOrFailedOrAborted = totalTests == 0 ? PlatformResources.ZeroTestsRan : passedOrFailedOrAborted;
                passedOrFailedOrAborted = _testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested ? PlatformResources.Aborted : passedOrFailedOrAborted;
                passedOrFailedOrAborted = totalTests < _minimumExpectedTest ? string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _minimumExpectedTest) : passedOrFailedOrAborted;
                ConsoleColor currentForeground = _console.GetForegroundColor();
                ConsoleColor consoleColor = runFailed ? ConsoleColor.Red : ConsoleColor.Green;
                try
                {
                    _console.SetForegroundColor(consoleColor);
                    _console.WriteLine($"{passedOrFailedOrAborted} - {PlatformResources.Failed}: {failedTests}, {PlatformResources.Passed}: {passedTests}, {PlatformResources.Skipped}: {skippedTests}, {PlatformResources.Total}: {totalTests}{(_testRequestExecutionTimeInfo is not null ? $", Duration: {ToHumanReadableDuration(_testRequestExecutionTimeInfo.Value.TimingInfo.Duration.TotalMilliseconds)}" : string.Empty)} - {Path.GetFileName(moduleOutput)} {(runtimeInformation is null ? string.Empty : $"({runtimeInformation})")}");
                }
                finally
                {
                    _console.SetForegroundColor(currentForeground);
                }

                if (_sessionFilesArtifact.Count <= 0 && _filesArtifact.Count <= 0)
                {
                    return;
                }

                if (!_underProcessMonitor)
                {
                    _console.WriteLine();
                }
            }

            StringBuilder artifacts = new();
            bool hasArtifacts = false;
            artifacts.AppendLine(_firstCallTo_OnSessionStartingAsync ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
            foreach (TestNodeFileArtifact testNodeFileArtifact in _sessionFilesArtifact.OfType<TestNodeFileArtifact>())
            {
                artifacts.AppendLine(CultureInfo.InvariantCulture, $"- {PlatformResources.ForTest} {testNodeFileArtifact.TestNode.DisplayName}: {testNodeFileArtifact.FileInfo.FullName}");
            }

            foreach (SessionFileArtifact sessionFileArtifact in _sessionFilesArtifact.Except(_sessionFilesArtifact.OfType<TestNodeFileArtifact>()))
            {
                artifacts.AppendLine(CultureInfo.InvariantCulture, $"- {sessionFileArtifact.FileInfo.FullName}");
                hasArtifacts = true;
            }

            foreach (FileArtifact fileArtifact in _filesArtifact)
            {
                artifacts.AppendLine(CultureInfo.InvariantCulture, $"- {fileArtifact.FileInfo.FullName}");
                hasArtifacts = true;
            }

            if (hasArtifacts)
            {
                _console.WriteLine(artifacts.ToString());
            }
        }
    }

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        // We implement IDataConsumerService and IOutputDisplayService.
        // So the engine is calling us before as IDataConsumerService and after as IOutputDisplayService.
        // The engine look for the ITestSessionLifetimeHandler in both case and call it.
        if (_firstCallTo_OnSessionStartingAsync)
        {
            _firstCallTo_OnSessionStartingAsync = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Displays provided data through IConsole, which is typically System.Console.
    /// </summary>
    /// <param name="producer">The producer that sent the data.</param>
    /// <param name="data">The data to be displayed.</param>
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data)
    {
        if (_isVSTestMode)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            switch (data)
            {
                case FormattedTextOutputDeviceData formattedTextOutputDeviceData:
                    {
                        ConsoleColor currentForegroundColor = _console.GetForegroundColor();
                        ConsoleColor currentGetBackgroundColor = _console.GetBackgroundColor();
                        try
                        {
                            if (formattedTextOutputDeviceData.BackgroundColor is SystemConsoleColor backgroundColor)
                            {
                                _console.SetBackgroundColor(backgroundColor.ConsoleColor);
                            }

                            if (formattedTextOutputDeviceData.ForegroundColor is SystemConsoleColor foregroundColor)
                            {
                                _console.SetForegroundColor(foregroundColor.ConsoleColor);
                            }

                            _console.WriteLine(formattedTextOutputDeviceData.Text);
                        }
                        finally
                        {
                            _console.SetBackgroundColor(currentGetBackgroundColor);
                            _console.SetForegroundColor(currentForegroundColor);
                        }

                        break;
                    }

                case TextOutputDeviceData textOutputDeviceData:
                    {
                        await LogDebugAsync(textOutputDeviceData.Text);
                        _console.WriteLine(textOutputDeviceData.Text);
                        break;
                    }

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    {
                        ConsoleColor currentForegroundColor = _console.GetForegroundColor();
                        try
                        {
                            _console.SetForegroundColor(ConsoleColor.Red);
                            await LogDebugAsync(exceptionOutputDeviceData.Exception.ToString());
                            _console.WriteLine(exceptionOutputDeviceData.Exception);
                        }
                        finally
                        {
                            _console.SetForegroundColor(currentForegroundColor);
                        }

                        break;
                    }
            }
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:
                TimingProperty? timingProperty = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TimingProperty>();
                string? duration = timingProperty is null ? null :
                    ToHumanReadableDuration(timingProperty.GlobalTiming.Duration.TotalMilliseconds);

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case ErrorTestNodeStateProperty errorState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: false,
                            duration: duration,
                            errorMessage: errorState.Exception?.Message ?? errorState.Explanation,
                            errorStackTrace: errorState.Exception?.StackTrace,
                            expected: null,
                            actual: null);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: false,
                            duration: duration,
                            errorMessage: failedState.Exception?.Message ?? failedState.Explanation,
                            errorStackTrace: failedState.Exception?.StackTrace,
                            expected: failedState.Exception?.Data["assert.expected"] as string,
                            actual: failedState.Exception?.Data["assert.actual"] as string);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: true,
                            duration: duration,
                            errorMessage: timeoutState.Exception?.Message ?? timeoutState.Explanation,
                            errorStackTrace: timeoutState.Exception?.StackTrace,
                            expected: null,
                            actual: null);
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        await HandleFailuresAsync(
                            testNodeStateChanged.TestNode.DisplayName,
                            isCancelled: true,
                            duration: duration,
                            errorMessage: cancelledState.Exception?.Message ?? cancelledState.Explanation,
                            errorStackTrace: cancelledState.Exception?.StackTrace,
                            expected: null,
                            actual: null);
                        break;

                    case PassedTestNodeStateProperty:
                        // In case of dotnet watch always display passed tests (it's slower to display but it's more user friendly).
                        // For other run, skip displaying passed tests.
                        if (_runtimeFeature.IsHotReloadEnabled)
                        {
                            await ConsoleWriteAsync(PlatformResources.PassedLowercase, ConsoleColor.DarkGreen);
                            await ConsoleWriteAsync($" {testNodeStateChanged.TestNode.DisplayName}");
                            await ConsoleWriteLineAsync($" {duration}", ConsoleColor.Gray);
                        }

                        Interlocked.Increment(ref _totalTests);
                        Interlocked.Increment(ref _totalPassedTests);
                        break;

                    case SkippedTestNodeStateProperty:
                        await ConsoleWriteAsync(PlatformResources.SkippedLowercase, ConsoleColor.Yellow);
                        await ConsoleWriteAsync($" {testNodeStateChanged.TestNode.DisplayName}");
                        await ConsoleWriteLineAsync($" {duration}", ConsoleColor.Gray);
                        Interlocked.Increment(ref _totalTests);
                        Interlocked.Increment(ref _totalSkippedTests);
                        break;
                }

                break;

            case SessionFileArtifact sessionFileArtifact:
                _sessionFilesArtifact.Add(sessionFileArtifact);
                break;
            case FileArtifact fileArtifact:
                _filesArtifact.Add(fileArtifact);
                break;
            case TestRequestExecutionTimeInfo testRequestExecutionTimeInfo:
                _testRequestExecutionTimeInfo = testRequestExecutionTimeInfo;
                break;
        }
    }

    protected virtual async Task HandleFailuresAsync(string testDisplayName, bool isCancelled, string? duration, string? errorMessage,
        string? errorStackTrace, string? expected, string? actual)
    {
        await ConsoleWriteAsync(PlatformResources.FailedLowercase, ConsoleColor.DarkRed);
        if (isCancelled)
        {
            await ConsoleWriteAsync($"({PlatformResources.CancelledLowercase})", ConsoleColor.DarkRed);
        }

        await ConsoleWriteAsync($" {testDisplayName}");
        if (duration != null)
        {
            await ConsoleWriteAsync($" {duration}", ConsoleColor.Gray);
        }

        await ConsoleWriteAsync(_environment.NewLine);
        await ConsoleWriteLineAsync(errorMessage, ConsoleColor.Red);

        if (expected is not null)
        {
            await ConsoleWriteLineAsync($"{PlatformResources.Expected}:", ConsoleColor.Red);
            await ConsoleWriteLineAsync(expected, ConsoleColor.Red);
        }

        if (actual is not null)
        {
            await ConsoleWriteLineAsync($"{PlatformResources.Actual}:", ConsoleColor.Red);
            await ConsoleWriteLineAsync(actual, ConsoleColor.Red);
        }

        if (errorStackTrace != null)
        {
            await ConsoleWriteLineAsync($"{PlatformResources.StackTrace}:", ConsoleColor.DarkRed);
            await ConsoleWriteLineAsync(errorStackTrace, ConsoleColor.DarkRed);
        }

        await ConsoleWriteAsync(_environment.NewLine);

        Interlocked.Increment(ref _totalTests);
        Interlocked.Increment(ref _totalFailedTests);
    }

    private async Task ConsoleWriteAsync(string? text, ConsoleColor? color = null)
    {
        if (_isVSTestMode)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            ConsoleColor currentForegroundColor = _console.GetForegroundColor();
            try
            {
                _console.SetForegroundColor(color ?? currentForegroundColor);
                _console.Write(text);
            }
            finally
            {
                _console.SetForegroundColor(currentForegroundColor);
            }
        }
    }

    private async Task ConsoleWriteLineAsync(string? text, ConsoleColor? color = null)
    {
        if (_isVSTestMode)
        {
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            ConsoleColor currentForegroundColor = _console.GetForegroundColor();
            try
            {
                _console.SetForegroundColor(color ?? currentForegroundColor);
                _console.WriteLine(text);
            }
            finally
            {
                _console.SetForegroundColor(currentForegroundColor);
            }
        }
    }

    /// <summary>
    /// Convert duration property text to human readable duration.
    /// </summary>
    internal /* for testing */ static string? ToHumanReadableDuration(double? durationInMs)
    {
        if (durationInMs is null or < 0)
        {
            return null;
        }

        var time = TimeSpan.FromMilliseconds(durationInMs.Value);

        StringBuilder stringBuilder = new();
        bool hasParentValue = false;

        if (time.Days > 0)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{time.Days}d");
            hasParentValue = true;
        }

        if (time.Hours > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Hours.ToString(CultureInfo.InvariantCulture))}h");
            hasParentValue = true;
        }

        if (time.Minutes > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Minutes.ToString(CultureInfo.InvariantCulture))}m");
            hasParentValue = true;
        }

        if (time.Seconds > 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Seconds.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0') : time.Seconds.ToString(CultureInfo.InvariantCulture))}s");
            hasParentValue = true;
        }

        if (time.Milliseconds >= 0 || hasParentValue)
        {
            stringBuilder.Append(CultureInfo.InvariantCulture, $"{(hasParentValue ? " " : string.Empty)}{(hasParentValue ? time.Milliseconds.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0') : time.Milliseconds.ToString(CultureInfo.InvariantCulture))}ms");
        }

        return stringBuilder.ToString();
    }
}

internal static class OutputFormatter
{
    public static string FormatString(string? text)
    {
        if (text == null)
        {
            return "<null>";
        }

        if (text == string.Empty)
        {
            return "<empty>";
        }

#pragma warning disable IDE0046 // Convert to conditional expression
        if (RoslynString.IsNullOrWhiteSpace(text))
        {
            return text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
#pragma warning restore IDE0046 // Convert to conditional expression

        return text;
    }
}
