// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Implementation of output device that writes to terminal with progress and optionally with ANSI.
/// </summary>
internal partial class TerminalOutputDevice : IPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IDisposable
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IProcessHandler _process;
    private readonly IPlatformInformation _platformInformation;
    private readonly bool _isVSTestMode;
    private readonly bool _isListTests;
    private readonly bool _isServerMode;
    private readonly ILogger? _logger;
    private readonly FileLoggerProvider? _fileLoggerProvider;
    private readonly IClock _clock;
    private readonly TerminalTestReporter _terminalTestReporter;
    private readonly string? _architecture;
    private readonly string? _targetFramework;
    private readonly string _assemblyName;
    private readonly char[] _dash = new char[] { '-' };

    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _bannerDisplayed;
    private TestRequestExecutionTimeInfo? _testRequestExecutionTimeInfo;

    public TerminalOutputDevice(ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource, IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, ITestHostControllerInfo testHostControllerInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler process, IPlatformInformation platformInformation,
        bool isVSTestMode, bool isListTests, bool isServerMode, int minimumExpectedTest, FileLoggerProvider? fileLoggerProvider, IClock clock, CommandLineParseResult parseResult)
    {
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _asyncMonitor = asyncMonitor;
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _process = process;
        _platformInformation = platformInformation;
        _isVSTestMode = isVSTestMode;
        _isListTests = isListTests;
        _isServerMode = isServerMode;
        _fileLoggerProvider = fileLoggerProvider;
        _clock = clock;

        if (_fileLoggerProvider is not null)
        {
            _logger = _fileLoggerProvider.CreateLogger(GetType().ToString());
        }

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#if !NETCOREAPP
            _architecture = GetShortArchitecture(RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant());
#else
            _architecture = GetShortArchitecture(RuntimeInformation.RuntimeIdentifier);
#endif
            _targetFramework = GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
        }

        _assemblyName = _testApplicationModuleInfo.GetCurrentTestApplicationFullPath();

        if (environment.GetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }

        bool noAnsi = parseResult.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);
        bool noProgress = parseResult.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption);

        Func<bool?> shouldShowProgress = noProgress
            // User preference is to not show progress.
            ? () => false
            // User preference is to allow showing progress, figure if we should actually show it based on whether or not we are a testhost controller.
            //
            // TestHost controller is not running any tests and it should not be writing progress.
            //
            // The test host controller info is not setup and populated until after this constructor, because it writes banner and then after it figures out if
            // the runner is a testHost controller, so we would always have it as null if we capture it directly. Instead we need to check it via
            // func.
            : () => testHostControllerInfo.IsCurrentProcessTestHostController == null ? null : !testHostControllerInfo.IsCurrentProcessTestHostController;

        // This is single exe run, don't show all the details of assemblies and their summaries
        // and don't show passed tests.
        bool verbose = false;
        _terminalTestReporter = new TerminalTestReporter(console, new()
        {
            BaseDirectory = null,
            ShowAssembly = verbose,
            ShowAssemblyStartAndComplete = verbose,
            ShowPassedTests = verbose,
            MinimumExpectedTests = minimumExpectedTest,
            UseAnsi = !noAnsi,
            ShowProgress = shouldShowProgress,
        });

        _testApplicationCancellationTokenSource.CancellationToken.Register(() => _terminalTestReporter.StartCancelling());
    }

    private static string? GetShortTargetFramework(string frameworkDescription)
    {
        // https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription?view=net-8.0
        string netFramework = ".NET Framework";
        if (frameworkDescription.StartsWith(netFramework, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Framework 4.7.2
            return frameworkDescription.Length > (netFramework.Length + 6)
                ? $"net{frameworkDescription[netFramework.Length + 1]}{frameworkDescription[netFramework.Length + 3]}{frameworkDescription[netFramework.Length + 5]}"
                : frameworkDescription;
        }

        string netCore = ".NET Core";
        if (frameworkDescription.StartsWith(netCore, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET Core 3.1
            return frameworkDescription.Length > (netCore.Length + 4)
                ? $"net{frameworkDescription[netCore.Length + 1]}{frameworkDescription[netCore.Length + 3]}"
                : frameworkDescription;
        }

        string net = ".NET";
        if (frameworkDescription.StartsWith(net, ignoreCase: false, CultureInfo.InvariantCulture))
        {
            // .NET 5
            return frameworkDescription.Length > (net.Length + 2)
                ? $"net{frameworkDescription[net.Length + 1]}"
                : frameworkDescription;
        }

        return frameworkDescription;
    }

    private string? GetShortArchitecture(string runtimeIdentifier)
        => runtimeIdentifier.Contains('-')
            ? runtimeIdentifier.Split(_dash, 2)[1]
            : runtimeIdentifier;

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
        typeof(TestRequestExecutionTimeInfo),
    ];

    /// <inheritdoc />
    public virtual string Uid { get; } = nameof(TerminalOutputDevice);

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
                    _terminalTestReporter.WriteMessage(bannerMessage);
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
                        stringBuilder.Append(CultureInfo.InvariantCulture, $" (UTC {buildDate.UtcDateTime.ToShortDateString()})");
                    }

                    if (_runtimeFeature.IsDynamicCodeSupported)
                    {
                        stringBuilder.Append(" [");
                        stringBuilder.Append(_architecture);
                        stringBuilder.Append(" - ");
                        stringBuilder.Append(_targetFramework);
                        stringBuilder.Append(']');
                    }

                    stringBuilder.AppendLine();
                    _terminalTestReporter.WriteMessage(stringBuilder.ToString());
                }
            }

            if (_fileLoggerProvider is not null)
            {
                if (_fileLoggerProvider.SyncFlush)
                {
                    _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _architecture, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerProvider.LogLevel, _fileLoggerProvider.FileLogger.FileName));
                }
                else
                {
                    _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _architecture, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerProvider.LogLevel, _fileLoggerProvider.FileLogger.FileName));
                }
            }
        }
    }

    public async Task DisplayBeforeSessionStartAsync()
    {
        // Start test execution here, rather than in ShowBanner, because then we know
        // if we are a testHost controller or not, and if we should show progress bar.
        _terminalTestReporter.TestExecutionStarted(_clock.UtcNow, workerCount: 1);
        _terminalTestReporter.AssemblyRunStarted(_assemblyName, _targetFramework, _architecture);
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
            if (!_firstCallTo_OnSessionStartingAsync)
            {
                _terminalTestReporter.AssemblyRunCompleted(_assemblyName, _targetFramework, _architecture);
                _terminalTestReporter.TestExecutionCompleted(_clock.UtcNow);
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
                    if (formattedTextOutputDeviceData.ForegroundColor is SystemConsoleColor color)
                    {
                        switch (color.ConsoleColor)
                        {
                            case ConsoleColor.Red:
                                _terminalTestReporter.WriteErrorMessage(_assemblyName, _targetFramework, _architecture, formattedTextOutputDeviceData.Text);
                                break;
                            case ConsoleColor.Yellow:
                                _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _architecture, formattedTextOutputDeviceData.Text);
                                break;
                            default:
                                _terminalTestReporter.WriteMessage(formattedTextOutputDeviceData.Text);
                                break;
                        }
                    }
                    else
                    {
                        _terminalTestReporter.WriteMessage(formattedTextOutputDeviceData.Text);
                    }

                    break;

                case TextOutputDeviceData textOutputDeviceData:
                    {
                        await LogDebugAsync(textOutputDeviceData.Text);
                        _terminalTestReporter.WriteMessage(textOutputDeviceData.Text);
                        break;
                    }

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    {
                        await LogDebugAsync(exceptionOutputDeviceData.Exception.ToString());
                        _terminalTestReporter.WriteErrorMessage(_assemblyName, _targetFramework, _architecture, exceptionOutputDeviceData.Exception);

                        break;
                    }
            }
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                TimeSpan duration = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration ?? TimeSpan.Zero;
                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case InProgressTestNodeStateProperty:
                        // do nothing.
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _architecture,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Error,
                            duration,
                            errorMessage: errorState.Exception?.Message ?? errorState.Explanation,
                            errorState.Exception?.StackTrace,
                            expected: null,
                            actual: null);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _architecture,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Fail,
                             duration,
                             errorMessage: failedState.Exception?.Message ?? failedState.Explanation,
                             failedState.Exception?.StackTrace,
                             expected: failedState.Exception?.Data["assert.expected"] as string,
                             actual: failedState.Exception?.Data["assert.actual"] as string);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _architecture,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Timeout,
                             duration,
                             errorMessage: timeoutState.Exception?.Message ?? timeoutState.Explanation,
                             timeoutState.Exception?.StackTrace,
                             expected: null,
                             actual: null);
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _architecture,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Canceled,
                             duration,
                             errorMessage: cancelledState.Exception?.Message ?? cancelledState.Explanation,
                             cancelledState.Exception?.StackTrace,
                             expected: null,
                             actual: null);
                        break;

                    case PassedTestNodeStateProperty:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _architecture,
                            testNodeStateChanged.TestNode.DisplayName,
                            outcome: TestOutcome.Passed,
                            duration: duration,
                            errorMessage: null,
                            errorStackTrace: null,
                            expected: null,
                            actual: null);
                        break;

                    case SkippedTestNodeStateProperty:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _architecture,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Skipped,
                            duration,
                            errorMessage: null,
                            errorStackTrace: null,
                            expected: null,
                            actual: null);
                        break;
                }

                break;

            case TestNodeFileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    _terminalTestReporter.ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _architecture,
                        artifact.TestNode.DisplayName,
                        artifact.FileInfo.FullName);
                }

                break;

            case SessionFileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    _terminalTestReporter.ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _architecture,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
            case FileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    _terminalTestReporter.ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _architecture,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
            case TestRequestExecutionTimeInfo testRequestExecutionTimeInfo:
                _testRequestExecutionTimeInfo = testRequestExecutionTimeInfo;
                break;
        }
    }

    public void Dispose()
        => _terminalTestReporter?.Dispose();
}
