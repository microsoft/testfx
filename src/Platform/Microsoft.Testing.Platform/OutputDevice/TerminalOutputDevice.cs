// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice.Terminal;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Implementation of output device that writes to terminal with progress and optionally with ANSI.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalOutputDevice : IHotReloadPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IDisposable,
    IAsyncInitializableExtension
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    private const char Dash = '-';

    private readonly IConsole _console;
    private readonly ITestHostControllerInfo _testHostControllerInfo;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IPlatformInformation _platformInformation;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IFileLoggerInformation? _fileLoggerInformation;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClock _clock;
    private readonly IStopPoliciesService _policiesService;
    private readonly string? _longArchitecture;
    private readonly string? _shortArchitecture;

    // The effective runtime that is executing the application e.g. .NET 9, when .NET 8 application is running with --roll-forward latest.
    private readonly string? _runtimeFramework;

    // The targeted framework, .NET 8 when application specifies <TargetFramework>net8.0</TargetFramework>
    private readonly string? _targetFramework;
    private readonly string _assemblyName;

    private TerminalTestReporter? _terminalTestReporter;
    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _bannerDisplayed;
    private bool _isListTests;
    private bool _isServerMode;
    private ILogger? _logger;

    public TerminalOutputDevice(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, ITestHostControllerInfo testHostControllerInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        ICommandLineOptions commandLineOptions, IFileLoggerInformation? fileLoggerInformation, ILoggerFactory loggerFactory, IClock clock,
        IStopPoliciesService policiesService)
    {
        _console = console;
        _testHostControllerInfo = testHostControllerInfo;
        _asyncMonitor = asyncMonitor;
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _platformInformation = platformInformation;
        _commandLineOptions = commandLineOptions;
        _fileLoggerInformation = fileLoggerInformation;
        _loggerFactory = loggerFactory;
        _clock = clock;
        _policiesService = policiesService;

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#if !NETCOREAPP
            _longArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            _shortArchitecture = GetShortArchitecture(_longArchitecture);
#else
            // RID has the operating system, we want to see that in the banner, but not next to every dll.
            _longArchitecture = RuntimeInformation.RuntimeIdentifier;
            _shortArchitecture = TerminalOutputDevice.GetShortArchitecture(RuntimeInformation.RuntimeIdentifier);
#endif
            _runtimeFramework = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
            _targetFramework = TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName) ?? _runtimeFramework;
        }

        _assemblyName = testApplicationModuleInfo.GetDisplayName();

        if (environment.GetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }
    }

    public async Task InitializeAsync()
    {
        await _policiesService.RegisterOnAbortCallbackAsync(
            () =>
            {
                _terminalTestReporter?.StartCancelling();
                return Task.CompletedTask;
            }).ConfigureAwait(false);

        if (_fileLoggerInformation is not null)
        {
            _logger = _loggerFactory.CreateLogger(GetType().ToString());
        }

        _isListTests = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
        _isServerMode = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey);
        bool noAnsi = _commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);

        // TODO: Replace this with proper CI detection that we already have in telemetry. https://github.com/microsoft/testfx/issues/5533#issuecomment-2838893327
        bool inCI = string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(_environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
        bool noProgress = _commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption);

        // _runtimeFeature.IsHotReloadEnabled is not set to true here, even if the session will be HotReload,
        // we need to postpone that decision until the first test result.
        //
        // This works but is NOT USED, we prefer to have the same experience of not showing passed tests in hotReload mode as in normal mode.
        // Func<bool> showPassed = () => _runtimeFeature.IsHotReloadEnabled;
        Func<bool> showPassed = () => false;
        bool outputOption = _commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.OutputOption, out string[]? arguments);
        if (outputOption && arguments?.Length > 0 && TerminalTestReporterCommandLineOptionsProvider.OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            showPassed = () => true;
        }

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
            : () => _isListTests || _isServerMode
                ? false
                : !_testHostControllerInfo.IsCurrentProcessTestHostController;

        // This is single exe run, don't show all the details of assemblies and their summaries.
        _terminalTestReporter = new TerminalTestReporter(_console, new()
        {
            BaseDirectory = null,
            ShowAssembly = false,
            ShowAssemblyStartAndComplete = false,
            ShowPassedTests = showPassed,
            MinimumExpectedTests = PlatformCommandLineProvider.GetMinimumExpectedTests(_commandLineOptions),
            UseAnsi = !noAnsi,
            UseCIAnsi = inCI,
            ShowActiveTests = true,
            ShowProgress = shouldShowProgress,
        });
    }

    private static string GetShortArchitecture(string runtimeIdentifier)
        => runtimeIdentifier.Contains(Dash)
            ? runtimeIdentifier.Split(Dash, 2)[1]
            : runtimeIdentifier;

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
    ];

    /// <inheritdoc />
    public string Uid => nameof(TerminalOutputDevice);

    /// <inheritdoc />
    public string Version => AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName => "Test Platform Console Service";

    /// <inheritdoc />
    public string Description => "Test Platform default console service";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogDebugAsync(string message)
    {
        if (_logger is not null)
        {
            await _logger.LogDebugAsync(message).ConfigureAwait(false);
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
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
                        stringBuilder.Append(_longArchitecture);
                        stringBuilder.Append(" - ");
                        stringBuilder.Append(_runtimeFramework);
                        stringBuilder.Append(']');
                    }

                    _terminalTestReporter.WriteMessage(stringBuilder.ToString());
                }
            }

            if (_fileLoggerInformation is not null)
            {
                if (_fileLoggerInformation.SyncronousWrite)
                {
                    _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
                else
                {
                    _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
            }
        }
    }

    public async Task DisplayBeforeHotReloadSessionStartAsync(CancellationToken cancellationToken)
        => await DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);

    public async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        if (_isServerMode)
        {
            return;
        }

        RoslynDebug.Assert(_terminalTestReporter is not null);

        // Start test execution here, rather than in ShowBanner, because then we know
        // if we are a testHost controller or not, and if we should show progress bar.
        _terminalTestReporter.TestExecutionStarted(_clock.UtcNow, workerCount: 1, isDiscovery: _isListTests);
        _terminalTestReporter.AssemblyRunStarted(_assemblyName, _targetFramework, _shortArchitecture);
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("DisplayBeforeSessionStartAsync").ConfigureAwait(false);
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync(CancellationToken cancellationToken)
        => await DisplayAfterSessionEndRunInternalAsync().ConfigureAwait(false);

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        if (_isServerMode)
        {
            return;
        }

        // Do NOT check and store the value in the constructor
        // it won't be populated yet, so you will always see false.
        if (_runtimeFeature.IsHotReloadEnabled)
        {
            return;
        }

        await DisplayAfterSessionEndRunInternalAsync().ConfigureAwait(false);
    }

    private async Task DisplayAfterSessionEndRunInternalAsync()
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (!_firstCallTo_OnSessionStartingAsync)
            {
                _terminalTestReporter.AssemblyRunCompleted(_assemblyName, _targetFramework, _shortArchitecture, exitCode: null, outputData: null, errorData: null);
                _terminalTestReporter.TestExecutionCompleted(_clock.UtcNow);
            }
        }
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (_isServerMode || testSessionContext.CancellationToken.IsCancellationRequested)
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
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            switch (data)
            {
                case FormattedTextOutputDeviceData formattedTextData:
                    await LogDebugAsync(formattedTextData.Text).ConfigureAwait(false);
                    _terminalTestReporter.WriteMessage(formattedTextData.Text, formattedTextData.ForegroundColor as SystemConsoleColor, formattedTextData.Padding);
                    break;

                case TextOutputDeviceData textData:
                    await LogDebugAsync(textData.Text).ConfigureAwait(false);
                    _terminalTestReporter.WriteMessage(textData.Text);
                    break;

                case WarningMessageOutputDeviceData warningData:
                    await LogDebugAsync(warningData.Message).ConfigureAwait(false);
                    _terminalTestReporter.WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, warningData.Message, null);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message).ConfigureAwait(false);
                    _terminalTestReporter.WriteErrorMessage(_assemblyName, _targetFramework, _shortArchitecture, errorData.Message, null);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    await LogDebugAsync(exceptionOutputDeviceData.Exception.ToString()).ConfigureAwait(false);
                    _terminalTestReporter.WriteErrorMessage(_assemblyName, _targetFramework, _shortArchitecture, exceptionOutputDeviceData.Exception);
                    break;
            }
        }
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        if (_isServerMode || cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                TimeSpan duration = testNodeStateChanged.TestNode.Properties.SingleOrDefault<TimingProperty>()?.GlobalTiming.Duration ?? TimeSpan.Zero;
                string? standardOutput = testNodeStateChanged.TestNode.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
                string? standardError = testNodeStateChanged.TestNode.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;

                foreach (FileArtifactProperty artifact in testNodeStateChanged.TestNode.Properties.OfType<FileArtifactProperty>())
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    _terminalTestReporter.ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _shortArchitecture,
                        testNodeStateChanged.TestNode.DisplayName,
                        artifact.FileInfo.FullName);
                }

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case InProgressTestNodeStateProperty:
                        _terminalTestReporter.TestInProgress(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName);
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Error,
                            duration,
                            null,
                            errorState.Explanation,
                            errorState.Exception,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case FailedTestNodeStateProperty failedState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             testNodeStateChanged.TestNode.Uid.Value,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Fail,
                             duration,
                             null,
                             failedState.Explanation,
                             failedState.Exception,
                             expected: failedState.Exception?.Data["assert.expected"] as string,
                             actual: failedState.Exception?.Data["assert.actual"] as string,
                             standardOutput,
                             standardError);
                        break;

                    case TimeoutTestNodeStateProperty timeoutState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             testNodeStateChanged.TestNode.Uid.Value,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Timeout,
                             duration,
                             null,
                             timeoutState.Explanation,
                             timeoutState.Exception,
                             expected: null,
                             actual: null,
                             standardOutput,
                             standardError);
                        break;

                    case CancelledTestNodeStateProperty cancelledState:
                        _terminalTestReporter.TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             testNodeStateChanged.TestNode.Uid.Value,
                             testNodeStateChanged.TestNode.DisplayName,
                             TestOutcome.Canceled,
                             duration,
                             null,
                             cancelledState.Explanation,
                             cancelledState.Exception,
                             expected: null,
                             actual: null,
                             standardOutput,
                             standardError);
                        break;

                    case PassedTestNodeStateProperty:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            outcome: TestOutcome.Passed,
                            duration: duration,
                            informativeMessage: null,
                            errorMessage: null,
                            exception: null,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case SkippedTestNodeStateProperty skippedState:
                        _terminalTestReporter.TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            TestOutcome.Skipped,
                            duration,
                            informativeMessage: skippedState.Explanation,
                            errorMessage: null,
                            exception: null,
                            expected: null,
                            actual: null,
                            standardOutput,
                            standardError);
                        break;

                    case DiscoveredTestNodeStateProperty:
                        _terminalTestReporter.TestDiscovered(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.DisplayName,
                            testNodeStateChanged.TestNode.Uid);
                        break;
                }

                break;

            case SessionFileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    _terminalTestReporter.ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _shortArchitecture,
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
                        _shortArchitecture,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
        => _terminalTestReporter?.Dispose();

    public async Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken)
    {
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
