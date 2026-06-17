// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
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
    IDisposable,
    IAsyncInitializableExtension
{
#pragma warning disable SA1310 // Field names should not contain underscore
    // Opt-in knobs (env vars only) for the silence-driven heartbeat renderer used in non-cursor modes.
    private const string MTP_PROGRESS_SILENCE_SECONDS = nameof(MTP_PROGRESS_SILENCE_SECONDS);
    private const string MTP_PROGRESS_SLOW_TEST_SECONDS = nameof(MTP_PROGRESS_SLOW_TEST_SECONDS);
#pragma warning restore SA1310 // Field names should not contain underscore

    private const char Dash = '-';

    // Guards the one-per-process deprecation warning emitted when the legacy --no-progress flag is used.
    private static int s_noProgressDeprecationWarningEmitted;

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
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly string? _longArchitecture;
    private readonly string? _shortArchitecture;

    // The effective runtime that is executing the application e.g. .NET 9, when .NET 8 application is running with --roll-forward latest.
    private readonly string? _runtimeFramework;

    // The targeted framework, .NET 8 when application specifies <TargetFramework>net8.0</TargetFramework>
    private readonly string? _targetFramework;
    private readonly string _assemblyName;

    // Buffer for discovered test nodes when --list-tests json is active. Writes happen only from
    // the message bus pump in ConsumeAsync (single-producer per consumer) and are fully drained
    // by the platform before DisplayAfterSessionEndRunInternalAsync runs, so no extra locking is
    // required. The list stays empty (and effectively unused) outside JSON mode.
    private readonly List<TestNode> _discoveredTestsForJson = [];

    private TerminalTestReporter? _terminalTestReporter;
    private bool _bannerDisplayed;
    private bool _isListTests;
    private bool _isListTestsJson;
    private bool _isServerMode;
    private bool _isAzureDevOpsEnvironment;
    private ILogger? _logger;
    private TestProcessRole? _processRole;

    public TerminalOutputDevice(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, ITestHostControllerInfo testHostControllerInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        ICommandLineOptions commandLineOptions, IFileLoggerInformation? fileLoggerInformation, ILoggerFactory loggerFactory, IClock clock,
        IStopPoliciesService policiesService, ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
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
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;

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

        if (environment.GetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }

        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    }

    public async Task InitializeAsync()
    {
        if (_fileLoggerInformation is not null)
        {
            _logger = _loggerFactory.CreateLogger(GetType().ToString());
        }

        _isListTests = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
        _isListTestsJson = PlatformCommandLineProvider.IsListTestsJsonOutput(_commandLineOptions);
        _isAzureDevOpsEnvironment = AzureDevOpsLogIssueFormatter.IsAzureDevOpsEnvironment(_environment);

        if (_isListTestsJson)
        {
            // In JSON discovery mode the standard abort callback would call
            // TerminalTestReporter.StartCancelling which writes "Cancelling test session" to
            // stdout, corrupting the JSON document. Route a single-line cancellation notice
            // to stderr instead so the user still gets feedback on Ctrl+C.
            await _policiesService.RegisterOnAbortCallbackAsync(
                async () =>
                {
                    await WriteToStandardErrorAsync(PlatformResources.CancellingTestSession).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(PlatformResources.PressCtrlCAgainToForceExit).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        else
        {
            await _policiesService.RegisterOnAbortCallbackAsync(
                () =>
                {
                    _terminalTestReporter?.StartCancelling();
                    return Task.CompletedTask;
                }).ConfigureAwait(false);
        }

        _isServerMode = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey);
        bool noAnsi = _commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);

        // --ansi <auto|on|off>, when present (with any value), takes precedence over the legacy --no-ansi flag.
        // This keeps the help text honest: passing --ansi at all overrides --no-ansi.
        AnsiOverride ansiOverride = AnsiOverride.None;
        if (_commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.AnsiOption, out string[]? ansiArguments)
            && ansiArguments is { Length: > 0 })
        {
            string ansiValue = ansiArguments[0];
            ansiOverride = CommandLineOptionArgumentValidator.IsOnValue(ansiValue)
                ? AnsiOverride.ForceOn
                : CommandLineOptionArgumentValidator.IsOffValue(ansiValue)
                    ? AnsiOverride.ForceOff
                    : AnsiOverride.Auto;
        }

        // When --ansi auto is explicitly specified, it overrides --no-ansi too.
        bool effectiveNoAnsi = noAnsi && ansiOverride == AnsiOverride.None;

        // Honor the de-facto cross-tool NO_COLOR convention (https://no-color.org): when present with any
        // non-empty value, suppress ANSI color/cursor codes unless the user explicitly opted in via
        // `--ansi on` (which still wins, matching the precedence documented in --help).
        bool noColorEnv = !RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable("NO_COLOR"));

        bool inCI = new CIEnvironmentDetector(_environment).IsCIEnvironment();
        bool isLLMEnvironment = new LLMEnvironmentDetector(_environment).IsLLMEnvironment();

        AnsiMode ansiMode = ansiOverride switch
        {
            // User explicitly forced ANSI on (e.g. `--ansi on`). Bypass CI / LLM / NO_COLOR / redirection
            // detection so colors and cursor movement are emitted even when stdout is redirected.
            AnsiOverride.ForceOn => AnsiMode.ForceAnsi,

            // User explicitly disabled ANSI (`--ansi off`).
            // Note that NoAnsi also implies no progress.
            AnsiOverride.ForceOff => AnsiMode.NoAnsi,

            // No --ansi argument was provided, or `--ansi auto` was provided.
            // Fall back to environment-based detection.
            // In LLM environments, prefer simple text output so that the LLM can parse it easily.
            // NO_COLOR (https://no-color.org) is honored as well: any non-empty value suppresses ANSI.
            _ when effectiveNoAnsi || noColorEnv || isLLMEnvironment => AnsiMode.NoAnsi,
            _ when inCI => AnsiMode.SimpleAnsi,
            _ => AnsiMode.AnsiIfPossible,
        };

        // --progress <auto|on|off> is the modern, positive form. When present it wins over the legacy
        // --no-progress flag. --no-progress keeps working as a deprecated alias for --progress off and
        // emits a single deprecation warning per process.
        bool noProgress;
        if (_commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.ProgressOption, out string[]? progressArguments)
            && progressArguments is { Length: > 0 })
        {
            // 'on' and 'auto' currently behave the same: progress is shown unless the terminal is not capable
            // of in-place updates (NoAnsi/SimpleAnsi) or we are a test-host controller, --list-tests, or server
            // mode. A dedicated heartbeat renderer for the non-cursor modes is tracked by #9125.
            noProgress = CommandLineOptionArgumentValidator.IsOffValue(progressArguments[0]);
        }
        else if (_commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption))
        {
            noProgress = true;
            if (Interlocked.Exchange(ref s_noProgressDeprecationWarningEmitted, 1) == 0)
            {
                await WriteToStandardErrorAsync(PlatformResources.TerminalNoProgressDeprecatedWarning).ConfigureAwait(false);
            }
        }
        else
        {
            noProgress = false;
        }

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

        OutputShowMode showStdout = GetShowOutputMode(_commandLineOptions, TerminalTestReporterCommandLineOptionsProvider.ShowStdoutOption, isLLMEnvironment);
        OutputShowMode showStderr = GetShowOutputMode(_commandLineOptions, TerminalTestReporterCommandLineOptionsProvider.ShowStderrOption, isLLMEnvironment);

        Func<bool?> shouldShowProgress = noProgress
            // User preference is to not show progress.
            ? static () => false
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
        _terminalTestReporter = new TerminalTestReporter(_assemblyName, _targetFramework, _shortArchitecture, _console, _testApplicationCancellationTokenSource, new()
        {
            ShowPassedTests = showPassed,
            MinimumExpectedTests = PlatformCommandLineProvider.GetMinimumExpectedTests(_commandLineOptions),
            AnsiMode = ansiMode,
            ShowActiveTests = true,
            ShowProgress = shouldShowProgress,
            ShowStdout = showStdout,
            ShowStderr = showStderr,
            HeartbeatSilenceThreshold = GetProgressThreshold(_environment, MTP_PROGRESS_SILENCE_SECONDS, defaultSeconds: 30),
            SlowTestThreshold = GetProgressThreshold(_environment, MTP_PROGRESS_SLOW_TEST_SECONDS, defaultSeconds: 60),
        });
    }

    // Reads an integer number of seconds from the given environment variable, falling back to
    // <paramref name="defaultSeconds"/> when unset or invalid. A value of 0 disables the related
    // heartbeat rule (returns TimeSpan.Zero). Negative or non-integer values are ignored.
    private static TimeSpan GetProgressThreshold(IEnvironment environment, string variableName, int defaultSeconds)
    {
        string? raw = environment.GetEnvironmentVariable(variableName);
        return !RoslynString.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(defaultSeconds);
    }

    // When the option is absent, default to OutputShowMode.Failed when running under a known
    // LLM/AI environment (less token noise for agents) and to OutputShowMode.All otherwise.
    // An explicit --show-stdout/--show-stderr value always wins over the LLM-aware default.
    // TODO(#8772): Update the --show-stdout/--show-stderr help text to reflect the LLM-aware default.
    private static OutputShowMode GetShowOutputMode(ICommandLineOptions commandLineOptions, string optionName, bool isLLMEnvironment)
        => commandLineOptions.TryGetOptionArgumentList(optionName, out string[]? arguments) && arguments is { Length: > 0 }
            ? arguments[0] switch
            {
                string s when TerminalTestReporterCommandLineOptionsProvider.ShowOutputFailedArgument.Equals(s, StringComparison.OrdinalIgnoreCase) => OutputShowMode.Failed,
                string s when TerminalTestReporterCommandLineOptionsProvider.ShowOutputNoneArgument.Equals(s, StringComparison.OrdinalIgnoreCase) => OutputShowMode.None,
                _ => OutputShowMode.All,
            }
            : isLLMEnvironment ? OutputShowMode.Failed : OutputShowMode.All;

    private enum AnsiOverride
    {
        /// <summary>
        /// The <c>--ansi</c> option was not provided.
        /// </summary>
        None,

        /// <summary>
        /// The <c>--ansi auto</c> option was provided. The user explicitly opts in to auto-detection
        /// (and out of the legacy <c>--no-ansi</c> flag if it was also passed).
        /// </summary>
        Auto,

        /// <summary>
        /// The <c>--ansi on</c> option was provided, forcing ANSI output regardless of environment.
        /// </summary>
        ForceOn,

        /// <summary>
        /// The <c>--ansi off</c> option was provided, disabling ANSI output.
        /// </summary>
        ForceOff,
    }

    private static string GetShortArchitecture(string runtimeIdentifier)
    {
        int firstIndexOfDash = runtimeIdentifier.IndexOf(Dash);
        return firstIndexOfDash < 0 ? runtimeIdentifier : runtimeIdentifier.Substring(firstIndexOfDash + 1);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(FileArtifact),
    ];

    /// <inheritdoc />
    public string Uid => nameof(TerminalOutputDevice);

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

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

    // Sole point that bypasses IConsole to reach stderr; used only by --list-tests json paths
    // so stdout stays reserved for the JSON document while errors and the cancellation notice
    // still surface somewhere. If IConsole ever grows a stderr abstraction, replace this helper.
    private static async Task WriteToStandardErrorAsync(string message)
        => await Console.Error.WriteLineAsync(message).ConfigureAwait(false);

    public async Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);

        if (_isListTestsJson)
        {
            // Machine-readable mode: keep stdout clean, suppress banner & file logger notice.
            // The env var propagates to any child test host the controller spawns so they also
            // stay quiet — important because the JSON document must be the sole stdout content.
            _bannerDisplayed = true;
            _environment.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");
            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (!_bannerDisplayed && !_isServerMode)
            {
                // skip the banner for the children processes
                _environment.SetEnvironmentVariable(OutputDeviceBannerHelper.TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER, "1");

                _bannerDisplayed = true;

                if (bannerMessage is not null)
                {
                    _terminalTestReporter.WriteMessage(bannerMessage);
                }
                else
                {
                    _terminalTestReporter.WriteMessage(OutputDeviceBannerHelper.BuildBannerText(_platformInformation, _runtimeFeature, _longArchitecture, _runtimeFramework));
                }
            }

            if (_fileLoggerInformation is not null)
            {
                if (_fileLoggerInformation.SynchronousWrite)
                {
                    _terminalTestReporter.WriteWarningMessage(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
                else
                {
                    _terminalTestReporter.WriteWarningMessage(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
            }
        }
    }

    public async Task DisplayBeforeHotReloadSessionStartAsync(CancellationToken cancellationToken)
        => await DisplayBeforeSessionStartAsync(cancellationToken).ConfigureAwait(false);

    public async Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken)
    {
        if (_isServerMode || _isListTestsJson)
        {
            return;
        }

        RoslynDebug.Assert(_terminalTestReporter is not null);

        // Start test execution here, rather than in ShowBanner, because then we know
        // if we are a testHost controller or not, and if we should show progress bar.
        _terminalTestReporter.TestExecutionStarted(_clock.UtcNow, workerCount: 1, isDiscovery: _isListTests);
        _terminalTestReporter.AssemblyRunStarted();
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("DisplayBeforeSessionStartAsync").ConfigureAwait(false);
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync(CancellationToken cancellationToken)
    {
        if (_isListTestsJson)
        {
            // JSON discovery is a point-in-time snapshot. Re-emitting after every hot-reload
            // cycle would produce multiple growing JSON documents on stdout, which would break
            // any consumer that pipes the output (the accumulated _discoveredTestsForJson buffer
            // would also re-include earlier tests every cycle).
            return;
        }

        await DisplayAfterSessionEndRunInternalAsync().ConfigureAwait(false);
    }

    public async Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken)
    {
        // Under --server (e.g. `dotnet test` with `--server dotnettestcli`) the terminal device stays
        // silent: discovered tests are streamed to the SDK through the dotnet-test pipe (see
        // DotnetTestDataConsumer), and the SDK owns rendering — including building the --list-tests json
        // document by combining the discovered tests from every test app into a single output.
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

        if (_isListTestsJson)
        {
            using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
            {
                if (_processRole == TestProcessRole.TestHost)
                {
                    _console.WriteLine(DiscoveredTestsJsonSerializer.Serialize(_discoveredTestsForJson));
                }
            }

            return;
        }

        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            if (_processRole == TestProcessRole.TestHost)
            {
                _terminalTestReporter.AssemblyRunCompleted();
                _terminalTestReporter.TestExecutionCompleted(_clock.UtcNow);
            }
            else
            {
                _terminalTestReporter.PrintOutOfProcessArtifacts();
            }
        }
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

        if (_isListTestsJson)
        {
            // Machine-readable mode: keep stdout reserved for the JSON document so consumers can
            // pipe it directly. Errors and exceptions still need surfacing somewhere, so route
            // them to stderr via WriteToStandardErrorAsync (the only place that bypasses IConsole,
            // which does not abstract stderr today). Azure Pipelines ##vso commands are skipped
            // here: they must be written to stdout to be processed, but stdout belongs to JSON.
            // Warnings and informational text are dropped to keep stdout strictly JSON.
            switch (data)
            {
                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(errorData.Message).ConfigureAwait(false);
                    break;

                case ExceptionOutputDeviceData exceptionData:
                    string exceptionText = exceptionData.Exception.ToString();
                    await LogDebugAsync(exceptionText).ConfigureAwait(false);
                    await WriteToStandardErrorAsync(exceptionText).ConfigureAwait(false);
                    break;
            }

            return;
        }

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
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityWarning, warningData.Message));
                    }

                    _terminalTestReporter.WriteWarningMessage(warningData.Message, null);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message).ConfigureAwait(false);
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityError, errorData.Message));
                    }

                    _terminalTestReporter.WriteErrorMessage(errorData.Message, null);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    string exceptionMessage = exceptionOutputDeviceData.Exception.ToString();
                    await LogDebugAsync(exceptionMessage).ConfigureAwait(false);
                    if (_isAzureDevOpsEnvironment)
                    {
                        _terminalTestReporter.WriteMessage(AzureDevOpsLogIssueFormatter.FormatLogIssue(AzureDevOpsLogIssueFormatter.SeverityError, exceptionMessage));
                    }

                    _terminalTestReporter.WriteErrorMessage(exceptionOutputDeviceData.Exception);
                    break;
            }
        }
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_terminalTestReporter is not null);
        cancellationToken.ThrowIfCancellationRequested();

        // Under --server (e.g. `dotnet test` with `--server dotnettestcli`) the terminal device does not
        // buffer or render anything: data flows to the SDK through the dotnet-test pipe instead, and the
        // SDK is responsible for producing the output (including the --list-tests json document).
        if (_isServerMode)
        {
            return Task.CompletedTask;
        }

        if (_isListTestsJson)
        {
            // Machine-readable mode: only buffer discovered tests, do not write anything to the terminal.
            if (value is TestNodeUpdateMessage testNodeUpdate
                && testNodeUpdate.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>() is DiscoveredTestNodeStateProperty)
            {
                _discoveredTestsForJson.Add(testNodeUpdate.TestNode);
            }

            return Task.CompletedTask;
        }

        switch (value)
        {
            case TestNodeUpdateMessage testNodeStateChanged:

                // Single-pass collection: replaces 3 × SingleOrDefault<T>() + 1 × OfType<T>() (4 O(n) traversals + 1 heap alloc)
                //   + 1 × SingleOrDefault<TestNodeStateProperty>() (O(1) fast path, now folded into the walk)
                // with one zero-allocation GetStructEnumerator() walk.
                TimingProperty? timing = null;
                StandardOutputProperty? stdoutProp = null;
                StandardErrorProperty? stderrProp = null;
                TestNodeStateProperty? nodeState = null;
                PropertyBag.PropertyBagEnumerator enumerator = testNodeStateChanged.TestNode.Properties.GetStructEnumerator();
                while (enumerator.MoveNext())
                {
                    switch (enumerator.Current)
                    {
                        case TimingProperty t: timing = t; break;
                        case StandardOutputProperty so: stdoutProp = so; break;
                        case StandardErrorProperty se: stderrProp = se; break;
                        case TestNodeStateProperty s: nodeState = s; break;
                        case FileArtifactProperty fa:
                            _terminalTestReporter.ArtifactAdded(
                                outOfProcess: _processRole != TestProcessRole.TestHost,
                                testNodeStateChanged.TestNode.DisplayName,
                                fa.FileInfo.FullName);
                            break;
                    }
                }

                TimeSpan? duration = timing?.GlobalTiming.Duration;
                string? standardOutput = stdoutProp?.StandardOutput;
                string? standardError = stderrProp?.StandardError;

                switch (nodeState)
                {
                    case InProgressTestNodeStateProperty:
                        _terminalTestReporter.TestInProgress(
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName);
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        _terminalTestReporter.TestCompleted(
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

#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
                    case CancelledTestNodeStateProperty cancelledState:
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
                        _terminalTestReporter.TestCompleted(
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
                        _terminalTestReporter.TestDiscovered(testNodeStateChanged.TestNode.DisplayName);
                        break;
                }

                break;

            case SessionFileArtifact artifact:
                {
                    _terminalTestReporter.ArtifactAdded(
                        outOfProcess: _processRole != TestProcessRole.TestHost,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
            case FileArtifact artifact:
                {
                    _terminalTestReporter.ArtifactAdded(
                        outOfProcess: _processRole != TestProcessRole.TestHost,
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
        _processRole = processRole;
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests)), cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
