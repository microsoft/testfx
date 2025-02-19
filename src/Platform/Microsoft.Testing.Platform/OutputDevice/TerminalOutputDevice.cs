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
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Implementation of output device that writes to terminal with progress and optionally with ANSI.
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed partial class TerminalOutputDevice :
    IHotReloadPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IDisposable,
    IAsyncInitializableExtension
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER = nameof(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER);
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>
    /// The two directory separator characters to be passed to methods like <see cref="string.IndexOfAny(char[])"/>.
    /// </summary>
    private static readonly string[] NewLineStrings = { "\r\n", "\n" };

    internal const string SingleIndentation = "  ";

    internal const string DoubleIndentation = $"{SingleIndentation}{SingleIndentation}";

    private readonly List<TestRunArtifact> _artifacts = new();
    private readonly ConcurrentDictionary<string, TestProgressState> _assemblies = new();
    private readonly string _assemblyName;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IClock _clock;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConsole _console;
    private readonly char[] _dash = new char[] { '-' };
    private readonly IEnvironment _environment;
    private readonly IFileLoggerInformation? _fileLoggerInformation;
    private readonly ILoggerFactory _loggerFactory;
    private readonly bool _isListTests;
    private readonly bool _isServerMode;
    private readonly bool _isVSTestMode;
    private readonly string? _longArchitecture;
    private readonly uint? _originalConsoleMode;
    private readonly IPlatformInformation _platformInformation;
    private readonly IStopPoliciesService _policiesService;
    private readonly IRuntimeFeature _runtimeFeature;
    // The effective runtime that is executing the application e.g. .NET 9, when .NET 8 application is running with --roll-forward latest.
    private readonly string? _runtimeFramework;
    private readonly string? _shortArchitecture;
    // The targeted framework, .NET 8 when application specifies <TargetFramework>net8.0</TargetFramework>
    private readonly string? _targetFramework;
    private readonly TestProgressStateAwareTerminal _terminalWithProgress;
    private readonly ITestHostControllerInfo _testHostControllerInfo;
    private readonly Func<IStopwatch> _createStopwatch;

    /// <summary>
    /// Gets path to which all other paths in output should be relative.
    /// </summary>
    private readonly string? _baseDirectory;

    /// <summary>
    /// Gets a value indicating whether we should show passed tests.
    /// </summary>
    private readonly Func<bool> _showPassedTests;

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly is the source of the data on screen. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    private readonly bool _showAssembly;

    /// <summary>
    /// Gets a value indicating whether we should show information about which assembly started or completed. Turn this off when running directly from an exe to reduce noise, because the path will always be the same.
    /// </summary>
    private readonly bool _showAssemblyStartAndComplete;

    /// <summary>
    /// Gets minimum amount of tests to run.
    /// </summary>
    private readonly int _minimumExpectedTests;

    /// <summary>
    /// Gets a value indicating whether we should write the progress periodically to screen. When ANSI is allowed we update the progress as often as we can. When ANSI is not allowed we update it every 3 seconds.
    /// This is a callback to nullable bool, because we don't know if we are running as test host controller until after we setup the console. So we should be polling for the value, until we get non-null boolean
    /// and then cache that value.
    /// </summary>
    private readonly Func<bool?> _showProgress;

    /// <summary>
    /// Gets a value indicating whether the active tests should be visible when the progress is shown.
    /// </summary>
    private readonly bool _showActiveTests;

    /// <summary>
    /// Gets a value indicating whether we should use ANSI escape codes or disable them. When true the capabilities of the console are autodetected.
    /// </summary>
    private readonly bool _useAnsi;

    private bool _bannerDisplayed;
    private int _buildErrorsCount;
    private int _counter;
    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _isDiscovery;
    private bool? _shouldShowPassedTests;
    private DateTimeOffset? _testExecutionStartTime;
    private DateTimeOffset? _testExecutionEndTime;
    private bool _wasCancelled;
    private ILogger? _logger;

    internal event EventHandler OnProgressStartUpdate
    {
        add
        {
            if (_terminalWithProgress is not null)
            {
                _terminalWithProgress.OnProgressStartUpdate += value;
            }
        }

        remove
        {
            if (_terminalWithProgress is not null)
            {
                _terminalWithProgress.OnProgressStartUpdate -= value;
            }
        }
    }

    internal event EventHandler OnProgressStopUpdate
    {
        add
        {
            if (_terminalWithProgress is not null)
            {
                _terminalWithProgress.OnProgressStopUpdate += value;
            }
        }

        remove
        {
            if (_terminalWithProgress is not null)
            {
                _terminalWithProgress.OnProgressStopUpdate -= value;
            }
        }
    }

    public TerminalOutputDevice(
        IConsole console, ITestApplicationModuleInfo testApplicationModuleInfo, ITestHostControllerInfo testHostControllerInfo,
        IAsyncMonitor asyncMonitor, IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        ICommandLineOptions commandLineOptions, IFileLoggerInformation? fileLoggerInformation, ILoggerFactory loggerFactory, IClock clock,
        IStopPoliciesService policiesService, bool? forceAnsi = null, Func<IStopwatch>? createStopwatch = null)
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
        _createStopwatch = createStopwatch ?? SystemStopwatch.StartNew;

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
            _shortArchitecture = GetShortArchitecture(RuntimeInformation.RuntimeIdentifier);
#endif
            _runtimeFramework = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);
            _targetFramework = TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName) ?? _runtimeFramework;
        }

        _assemblyName = testApplicationModuleInfo.GetDisplayName();
        _isVSTestMode = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey);
        _isListTests = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey);
        _isServerMode = _commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey);

        if (environment.GetEnvironmentVariable(TESTINGPLATFORM_CONSOLEOUTPUTDEVICE_SKIP_BANNER) is not null)
        {
            _bannerDisplayed = true;
        }

        // _runtimeFeature.IsHotReloadEnabled is not set to true here, even if the session will be HotReload,
        // we need to postpone that decision until the first test result.
        //
        // This works but is NOT USED, we prefer to have the same experience of not showing passed tests in hotReload mode as in normal mode.
        // Func<bool> showPassed = () => _runtimeFeature.IsHotReloadEnabled;
        Func<bool> showPassed = () => false;
        bool outputOption = _commandLineOptions.TryGetOptionArgumentList(TerminalTestReporterCommandLineOptionsProvider.OutputOption, out string[]? arguments);
        if (outputOption && arguments?.Length > 0
            && TerminalTestReporterCommandLineOptionsProvider.OutputOptionDetailedArgument.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
        {
            showPassed = () => true;
        }

        _showProgress = _commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoProgressOption)
            // User preference is to not show progress.
            ? () => false
            // User preference is to allow showing progress, figure if we should actually show it based on whether or not we are a testhost controller.
            //
            // TestHost controller is not running any tests and it should not be writing progress.
            //
            // The test host controller info is not setup and populated until after this constructor, because it writes banner and then after it figures out if
            // the runner is a testHost controller, so we would always have it as null if we capture it directly. Instead we need to check it via
            // func.
            : () => _isVSTestMode || _isListTests || _isServerMode
                ? false
                : !_testHostControllerInfo.IsCurrentProcessTestHostController;

        _baseDirectory = null;
        _showAssembly = false;
        _showAssemblyStartAndComplete = false;
        _showPassedTests = showPassed;
        _minimumExpectedTests = PlatformCommandLineProvider.GetMinimumExpectedTests(_commandLineOptions);
        _useAnsi = !_commandLineOptions.IsOptionSet(TerminalTestReporterCommandLineOptionsProvider.NoAnsiOption);
        _showActiveTests = true;

        // When not writing to ANSI we write the progress to screen and leave it there so we don't want to write it more often than every few seconds.
        int nonAnsiUpdateCadenceInMs = 3_000;
        // When writing to ANSI we update the progress in place and it should look responsive so we update every half second, because we only show seconds on the screen, so it is good enough.
        int ansiUpdateCadenceInMs = 500;

        if (!_useAnsi || forceAnsi is false)
        {
            _terminalWithProgress = new TestProgressStateAwareTerminal(
                new NonAnsiTerminal(_console), _showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }
        else
        {
            // Autodetect.
            (bool consoleAcceptsAnsiCodes, bool _, _originalConsoleMode) = NativeMethods.QueryIsScreenAndTryEnableAnsiColorCodes();
            _terminalWithProgress = consoleAcceptsAnsiCodes || forceAnsi is true
                ? new TestProgressStateAwareTerminal(
                    new AnsiTerminal(_console, _baseDirectory), _showProgress, writeProgressImmediatelyAfterOutput: true, updateEvery: ansiUpdateCadenceInMs)
                : new TestProgressStateAwareTerminal(
                    new NonAnsiTerminal(_console), _showProgress, writeProgressImmediatelyAfterOutput: false, updateEvery: nonAnsiUpdateCadenceInMs);
        }
    }

    public async Task InitializeAsync()
    {
        await _policiesService.RegisterOnAbortCallbackAsync(
                () =>
                {
                    StartCancelling();
                    return Task.CompletedTask;
                });

        if (_fileLoggerInformation is not null)
        {
            _logger = _loggerFactory.CreateLogger(GetType().ToString());
        }
    }

    private string GetShortArchitecture(string runtimeIdentifier)
        => runtimeIdentifier.Contains('-')
            ? runtimeIdentifier.Split(_dash, 2)[1]
            : runtimeIdentifier;

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact),
        typeof(TestNodeFileArtifact),
        typeof(FileArtifact),
    ];

    /// <inheritdoc />
    public string Uid { get; } = nameof(TerminalOutputDevice);

    /// <inheritdoc />
    public string Version { get; } = AppVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = "Test Platform Console Service";

    /// <inheritdoc />
    public string Description { get; } = "Test Platform default console service";

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    private async Task LogDebugAsync(string message)
    {
        if (_logger is not null)
        {
            await _logger.LogDebugAsync(message);
        }
    }

    public async Task DisplayBannerAsync(string? bannerMessage)
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
                    WriteMessage(bannerMessage);
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

                    WriteMessage(stringBuilder.ToString());
                }
            }

            if (_fileLoggerInformation is not null)
            {
                if (_fileLoggerInformation.SyncronousWrite)
                {
                    WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
                else
                {
                    WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, string.Format(CultureInfo.CurrentCulture, PlatformResources.DiagnosticFileLevelWithAsyncFlush, _fileLoggerInformation.LogLevel, _fileLoggerInformation.LogFile.FullName), padding: null);
                }
            }
        }
    }

    public async Task DisplayBeforeHotReloadSessionStartAsync()
        => await DisplayBeforeSessionStartAsync();

    public async Task DisplayBeforeSessionStartAsync()
    {
        if (_isServerMode)
        {
            return;
        }

        // Start test execution here, rather than in ShowBanner, because then we know
        // if we are a testHost controller or not, and if we should show progress bar.
        TestExecutionStarted(_clock.UtcNow, workerCount: 1, isDiscovery: false);
        AssemblyRunStarted(_assemblyName, _targetFramework, _shortArchitecture, executionId: null);
        if (_logger is not null && _logger.IsEnabled(LogLevel.Trace))
        {
            await _logger.LogTraceAsync("DisplayBeforeSessionStartAsync");
        }
    }

    public async Task DisplayAfterHotReloadSessionEndAsync()
        => await DisplayAfterSessionEndRunInternalAsync();

    public async Task DisplayAfterSessionEndRunAsync()
    {
        if (_isVSTestMode || _isListTests || _isServerMode)
        {
            return;
        }

        // Do NOT check and store the value in the constructor
        // it won't be populated yet, so you will always see false.
        if (_runtimeFeature.IsHotReloadEnabled)
        {
            return;
        }

        await DisplayAfterSessionEndRunInternalAsync();
    }

    private async Task DisplayAfterSessionEndRunInternalAsync()
    {
        using (await _asyncMonitor.LockAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            if (!_firstCallTo_OnSessionStartingAsync)
            {
                AssemblyRunCompleted(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, exitCode: null, outputData: null, errorData: null);
                TestExecutionCompleted(_clock.UtcNow);
            }
        }
    }

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
    {
        if (_isServerMode || cancellationToken.IsCancellationRequested)
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
                case FormattedTextOutputDeviceData formattedTextData:
                    await LogDebugAsync(formattedTextData.Text);
                    WriteMessage(formattedTextData.Text, formattedTextData.ForegroundColor as SystemConsoleColor, formattedTextData.Padding);
                    break;

                case TextOutputDeviceData textData:
                    await LogDebugAsync(textData.Text);
                    WriteMessage(textData.Text);
                    break;

                case WarningMessageOutputDeviceData warningData:
                    await LogDebugAsync(warningData.Message);
                    WriteWarningMessage(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, warningData.Message, null);
                    break;

                case ErrorMessageOutputDeviceData errorData:
                    await LogDebugAsync(errorData.Message);
                    WriteErrorMessage(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, errorData.Message, null);
                    break;

                case ExceptionOutputDeviceData exceptionOutputDeviceData:
                    await LogDebugAsync(exceptionOutputDeviceData.Exception.ToString());
                    WriteErrorMessage(_assemblyName, _targetFramework, _shortArchitecture, executionId: null, exceptionOutputDeviceData.Exception);
                    break;
            }
        }
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
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

                switch (testNodeStateChanged.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>())
                {
                    case InProgressTestNodeStateProperty:
                        TestInProgress(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            testNodeStateChanged.TestNode.Uid.Value,
                            testNodeStateChanged.TestNode.DisplayName,
                            executionId: null);
                        break;

                    case ErrorTestNodeStateProperty errorState:
                        TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            executionId: null,
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
                        TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             executionId: null,
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
                        TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             executionId: null,
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
                        TestCompleted(
                             _assemblyName,
                             _targetFramework,
                             _shortArchitecture,
                             executionId: null,
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
                        TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            executionId: null,
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
                        TestCompleted(
                            _assemblyName,
                            _targetFramework,
                            _shortArchitecture,
                            executionId: null,
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
                }

                break;

            case TestNodeFileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _shortArchitecture,
                        executionId: null,
                        artifact.TestNode.DisplayName,
                        artifact.FileInfo.FullName);
                }

                break;

            case SessionFileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _shortArchitecture,
                        executionId: null,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
            case FileArtifact artifact:
                {
                    bool isOutOfProcessArtifact = _firstCallTo_OnSessionStartingAsync;
                    ArtifactAdded(
                        isOutOfProcessArtifact,
                        _assemblyName,
                        _targetFramework,
                        _shortArchitecture,
                        executionId: null,
                        testName: null,
                        artifact.FileInfo.FullName);
                }

                break;
        }

        return Task.CompletedTask;
    }

    public async Task HandleProcessRoleAsync(TestProcessRole processRole)
    {
        if (processRole == TestProcessRole.TestHost)
        {
            await _policiesService.RegisterOnMaxFailedTestsCallbackAsync(
                async (maxFailedTests, _) => await DisplayAsync(
                    this, new TextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.ReachedMaxFailedTestsMessage, maxFailedTests))));
        }
    }

#if NET7_0_OR_GREATER
    // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
    // thread suspends, so the regex gets blamed incorrectly.
    [GeneratedRegex(@"^   at ((?<code>.+) in (?<file>.+):line (?<line>\d+)|(?<code1>.+))$", RegexOptions.ExplicitCapture)]
    private static partial Regex GetFrameRegex();
#else
    private static Regex? s_regex;

    [MemberNotNull(nameof(s_regex))]
    private static Regex GetFrameRegex()
    {
        if (s_regex != null)
        {
            return s_regex;
        }

        string atResourceName = "Word_At";
        string inResourceName = "StackTrace_InFileLineNumber";

        string? atString = null;
        string? inString = null;

        // Grab words from localized resource, in case the stack trace is localized.
        try
        {
            // Get these resources: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/Resources/Strings.resx
#pragma warning disable RS0030 // Do not use banned APIs
            MethodInfo? getResourceStringMethod = typeof(Environment).GetMethod(
                "GetResourceString",
                BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null);
#pragma warning restore RS0030 // Do not use banned APIs
            if (getResourceStringMethod is not null)
            {
                // <value>at</value>
                atString = (string?)getResourceStringMethod.Invoke(null, [atResourceName]);

                // <value>in {0}:line {1}</value>
                inString = (string?)getResourceStringMethod.Invoke(null, [inResourceName]);
            }
        }
        catch
        {
            // If we fail, populate the defaults below.
        }

        atString = atString == null || atString == atResourceName ? "at" : atString;
        inString = inString == null || inString == inResourceName ? "in {0}:line {1}" : inString;

        string inPattern = string.Format(CultureInfo.InvariantCulture, inString, "(?<file>.+)", @"(?<line>\d+)");

        // Specifying no timeout, the regex is linear. And the timeout does not measure the regex only, but measures also any
        // thread suspends, so the regex gets blamed incorrectly.
        s_regex = new Regex(@$"^   {atString} ((?<code>.+) {inPattern}|(?<code1>.+))$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        return s_regex;
    }
#endif

    internal /* for testing */ void TestExecutionStarted(DateTimeOffset testStartTime, int workerCount, bool isDiscovery)
    {
        _isDiscovery = isDiscovery;
        _testExecutionStartTime = testStartTime;
        _terminalWithProgress.StartShowingProgress(workerCount);
    }

    internal /* for testing */ void AssemblyRunStarted(string assembly, string? targetFramework, string? architecture, string? executionId)
    {
        if (_showAssembly && _showAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.Append(_isDiscovery ? PlatformResources.DiscoveringTestsFrom : PlatformResources.RunningTestsFrom);
                terminal.Append(' ');
                AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
                terminal.AppendLine();
            });
        }

        GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
    }

    private TestProgressState GetOrAddAssemblyRun(string assembly, string? targetFramework, string? architecture, string? executionId)
    {
        string key = $"{assembly}|{targetFramework}|{architecture}|{executionId}";
        return _assemblies.GetOrAdd(key, _ =>
        {
            IStopwatch sw = _createStopwatch();
            var assemblyRun = new TestProgressState(Interlocked.Increment(ref _counter), assembly, targetFramework, architecture, sw);
            int slotIndex = _terminalWithProgress.AddWorker(assemblyRun);
            assemblyRun.SlotIndex = slotIndex;

            return assemblyRun;
        });
    }

    internal /* for testing */ void TestExecutionCompleted(DateTimeOffset endTime)
    {
        _testExecutionEndTime = endTime;
        _terminalWithProgress.StopShowingProgress();

        _terminalWithProgress.WriteToTerminal(_isDiscovery ? AppendTestDiscoverySummary : AppendTestRunSummary);

        NativeMethods.RestoreConsoleMode(_originalConsoleMode);
        _assemblies.Clear();
        _buildErrorsCount = 0;
        _testExecutionStartTime = null;
        _testExecutionEndTime = null;
    }

    private void AppendTestRunSummary(ITerminal terminal)
    {
        terminal.AppendLine();

        IEnumerable<IGrouping<bool, TestRunArtifact>> artifactGroups = _artifacts.GroupBy(a => a.OutOfProcess);
        if (artifactGroups.Any())
        {
            terminal.AppendLine();
        }

        foreach (IGrouping<bool, TestRunArtifact> artifactGroup in artifactGroups)
        {
            terminal.Append(SingleIndentation);
            terminal.AppendLine(artifactGroup.Key ? PlatformResources.OutOfProcessArtifactsProduced : PlatformResources.InProcessArtifactsProduced);
            foreach (TestRunArtifact artifact in artifactGroup)
            {
                terminal.Append(DoubleIndentation);
                terminal.Append("- ");
                if (!RoslynString.IsNullOrWhiteSpace(artifact.TestName))
                {
                    terminal.Append(PlatformResources.ForTest);
                    terminal.Append(" '");
                    terminal.Append(artifact.TestName);
                    terminal.Append("': ");
                }

                terminal.AppendLink(artifact.Path, lineNumber: null);
                terminal.AppendLine();
            }
        }

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        int totalFailedTests = _assemblies.Values.Sum(a => a.FailedTests);
        int totalSkippedTests = _assemblies.Values.Sum(a => a.SkippedTests);

        bool notEnoughTests = totalTests < _minimumExpectedTests;
        bool allTestsWereSkipped = totalTests == 0 || totalTests == totalSkippedTests;
        bool anyTestFailed = totalFailedTests > 0;
        bool runFailed = anyTestFailed || notEnoughTests || allTestsWereSkipped || _wasCancelled;
        terminal.SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green);

        terminal.Append(PlatformResources.TestRunSummary);
        terminal.Append(' ');

        if (_wasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
        }
        else if (notEnoughTests)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.MinimumExpectedTestsPolicyViolation, totalTests, _minimumExpectedTests));
        }
        else if (allTestsWereSkipped)
        {
            terminal.Append(PlatformResources.ZeroTestsRan);
        }
        else if (anyTestFailed)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Failed));
        }
        else
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, "{0}!", PlatformResources.Passed));
        }

        if (!_showAssembly && _assemblies.Count == 1)
        {
            TestProgressState testProgressState = _assemblies.Values.Single();
            terminal.SetColor(TerminalColor.DarkGray);
            terminal.Append(" - ");
            terminal.ResetColor();
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, testProgressState.Assembly, testProgressState.TargetFramework, testProgressState.Architecture);
        }

        terminal.AppendLine();

        if (_showAssembly && _assemblies.Count > 1)
        {
            foreach (TestProgressState assemblyRun in _assemblies.Values)
            {
                terminal.Append(SingleIndentation);
                AppendAssemblySummary(assemblyRun, terminal);
                terminal.AppendLine();
            }

            terminal.AppendLine();
        }

        int total = _assemblies.Values.Sum(t => t.TotalTests);
        int failed = _assemblies.Values.Sum(t => t.FailedTests);
        int passed = _assemblies.Values.Sum(t => t.PassedTests);
        int skipped = _assemblies.Values.Sum(t => t.SkippedTests);
        TimeSpan runDuration = _testExecutionStartTime != null && _testExecutionEndTime != null ? (_testExecutionEndTime - _testExecutionStartTime).Value : TimeSpan.Zero;

        bool colorizeFailed = failed > 0;
        bool colorizePassed = passed > 0 && _buildErrorsCount == 0 && failed == 0;
        bool colorizeSkipped = skipped > 0 && skipped == total && _buildErrorsCount == 0 && failed == 0;

        string totalText = $"{SingleIndentation}total: {total}";
        string failedText = $"{SingleIndentation}failed: {failed}";
        string passedText = $"{SingleIndentation}succeeded: {passed}";
        string skippedText = $"{SingleIndentation}skipped: {skipped}";
        string durationText = $"{SingleIndentation}duration: ";

        terminal.ResetColor();
        terminal.AppendLine(totalText);
        if (colorizeFailed)
        {
            terminal.SetColor(TerminalColor.Red);
        }

        terminal.AppendLine(failedText);

        if (colorizeFailed)
        {
            terminal.ResetColor();
        }

        if (colorizePassed)
        {
            terminal.SetColor(TerminalColor.Green);
        }

        terminal.AppendLine(passedText);

        if (colorizePassed)
        {
            terminal.ResetColor();
        }

        if (colorizeSkipped)
        {
            terminal.SetColor(TerminalColor.Yellow);
        }

        terminal.AppendLine(skippedText);

        if (colorizeSkipped)
        {
            terminal.ResetColor();
        }

        terminal.Append(durationText);
        AppendLongDuration(terminal, runDuration, wrapInParentheses: false, colorize: false);
        terminal.AppendLine();
    }

    /// <summary>
    /// Print a build result summary to the output.
    /// </summary>
    private static void AppendAssemblyResult(ITerminal terminal, bool succeeded, int countErrors, int countWarnings)
    {
        if (!succeeded)
        {
            terminal.SetColor(TerminalColor.Red);
            // If the build failed, we print one of three red strings.
            string text = (countErrors > 0, countWarnings > 0) switch
            {
                (true, true) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithErrorsAndWarnings, countErrors, countWarnings),
                (true, _) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithErrors, countErrors),
                (false, true) => string.Format(CultureInfo.CurrentCulture, PlatformResources.FailedWithWarnings, countWarnings),
                _ => PlatformResources.FailedLowercase,
            };
            terminal.Append(text);
            terminal.ResetColor();
        }
        else if (countWarnings > 0)
        {
            terminal.SetColor(TerminalColor.Yellow);
            terminal.Append($"succeeded with {countWarnings} warning(s)");
            terminal.ResetColor();
        }
        else
        {
            terminal.SetColor(TerminalColor.Green);
            terminal.Append(PlatformResources.PassedLowercase);
            terminal.ResetColor();
        }
    }

    internal void TestCompleted(
       string assembly,
       string? targetFramework,
       string? architecture,
       string? executionId,
       string testNodeUid,
       string displayName,
       TestOutcome outcome,
       TimeSpan duration,
       string? informativeMessage,
       string? errorMessage,
       Exception? exception,
       string? expected,
       string? actual,
       string? standardOutput,
       string? errorOutput)
    {
        FlatException[] flatExceptions = ExceptionFlattener.Flatten(errorMessage, exception);
        TestCompleted(
            assembly,
            targetFramework,
            architecture,
            executionId,
            testNodeUid,
            displayName,
            outcome,
            duration,
            informativeMessage,
            flatExceptions,
            expected,
            actual,
            standardOutput,
            errorOutput);
    }

    private void TestCompleted(
        string assembly,
        string? targetFramework,
        string? architecture,
        string? executionId,
        string testNodeUid,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? informativeMessage,
        FlatException[] exceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        if (_showActiveTests)
        {
            asm.TestNodeResultsState?.RemoveRunningTestNode(testNodeUid);
        }

        switch (outcome)
        {
            case TestOutcome.Error:
            case TestOutcome.Timeout:
            case TestOutcome.Canceled:
            case TestOutcome.Fail:
                asm.FailedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Passed:
                asm.PassedTests++;
                asm.TotalTests++;
                break;
            case TestOutcome.Skipped:
                asm.SkippedTests++;
                asm.TotalTests++;
                break;
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
        if (outcome != TestOutcome.Passed || GetShowPassedTests())
        {
            _terminalWithProgress.WriteToTerminal(terminal => RenderTestCompleted(
                terminal,
                assembly,
                targetFramework,
                architecture,
                displayName,
                outcome,
                duration,
                informativeMessage,
                exceptions,
                expected,
                actual,
                standardOutput,
                errorOutput));
        }
    }

    private bool GetShowPassedTests()
    {
        _shouldShowPassedTests ??= _showPassedTests();
        return _shouldShowPassedTests.Value;
    }

    private void RenderTestCompleted(
        ITerminal terminal,
        string assembly,
        string? targetFramework,
        string? architecture,
        string displayName,
        TestOutcome outcome,
        TimeSpan duration,
        string? informativeMessage,
        FlatException[] flatExceptions,
        string? expected,
        string? actual,
        string? standardOutput,
        string? errorOutput)
    {
        if (outcome == TestOutcome.Passed && !GetShowPassedTests())
        {
            return;
        }

        TerminalColor color = outcome switch
        {
            TestOutcome.Error or TestOutcome.Fail or TestOutcome.Canceled or TestOutcome.Timeout => TerminalColor.Red,
            TestOutcome.Skipped => TerminalColor.Yellow,
            TestOutcome.Passed => TerminalColor.Green,
            _ => throw new NotSupportedException(),
        };
        string outcomeText = outcome switch
        {
            TestOutcome.Fail or TestOutcome.Error => PlatformResources.FailedLowercase,
            TestOutcome.Skipped => PlatformResources.SkippedLowercase,
            TestOutcome.Canceled or TestOutcome.Timeout => $"{PlatformResources.FailedLowercase} ({PlatformResources.CancelledLowercase})",
            TestOutcome.Passed => PlatformResources.PassedLowercase,
            _ => throw new NotSupportedException(),
        };

        terminal.SetColor(color);
        terminal.Append(outcomeText);
        terminal.ResetColor();
        terminal.Append(' ');
        terminal.Append(displayName);
        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(' ');
        AppendLongDuration(terminal, duration);
        if (_showAssembly)
        {
            terminal.AppendLine();
            terminal.Append(SingleIndentation);
            terminal.Append(PlatformResources.FromFile);
            terminal.Append(' ');
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly, targetFramework, architecture);
        }

        terminal.AppendLine();

        AppendIndentedLine(terminal, informativeMessage, SingleIndentation);
        FormatErrorMessage(terminal, flatExceptions, outcome, 0);
        FormatExpectedAndActual(terminal, expected, actual);
        FormatStackTrace(terminal, flatExceptions, 0);
        FormatInnerExceptions(terminal, flatExceptions);
        FormatStandardAndErrorOutput(terminal, standardOutput, errorOutput);
    }

    private static void FormatInnerExceptions(ITerminal terminal, FlatException[] exceptions)
    {
        if (exceptions is null || exceptions.Length == 0)
        {
            return;
        }

        for (int i = 1; i < exceptions.Length; i++)
        {
            terminal.SetColor(TerminalColor.Red);
            terminal.Append(SingleIndentation);
            terminal.Append("--->");
            FormatErrorMessage(terminal, exceptions, TestOutcome.Error, i);
            FormatStackTrace(terminal, exceptions, i);
        }
    }

    private static void FormatErrorMessage(ITerminal terminal, FlatException[] exceptions, TestOutcome outcome, int index)
    {
        string? firstErrorMessage = GetStringFromIndexOrDefault(exceptions, e => e.ErrorMessage, index);
        string? firstErrorType = GetStringFromIndexOrDefault(exceptions, e => e.ErrorType, index);
        string? firstStackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (RoslynString.IsNullOrWhiteSpace(firstErrorMessage) && RoslynString.IsNullOrWhiteSpace(firstErrorType) && RoslynString.IsNullOrWhiteSpace(firstStackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);

        if (firstStackTrace is null)
        {
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else if (outcome == TestOutcome.Fail)
        {
            // For failed tests, we don't prefix the message with the exception type because it is most likely an assertion specific exception like AssertionFailedException, and we prefer to show that without the exception type to avoid additional noise.
            AppendIndentedLine(terminal, firstErrorMessage, SingleIndentation);
        }
        else
        {
            AppendIndentedLine(terminal, $"{firstErrorType}: {firstErrorMessage}", SingleIndentation);
        }

        terminal.ResetColor();
    }

    private static string? GetStringFromIndexOrDefault(FlatException[] exceptions, Func<FlatException, string?> property, int index) =>
        exceptions != null && exceptions.Length >= index + 1 ? property(exceptions[index]) : null;

    private static void FormatExpectedAndActual(ITerminal terminal, string? expected, string? actual)
    {
        if (RoslynString.IsNullOrWhiteSpace(expected) && RoslynString.IsNullOrWhiteSpace(actual))
        {
            return;
        }

        terminal.SetColor(TerminalColor.Red);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Expected);
        AppendIndentedLine(terminal, expected, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.Actual);
        AppendIndentedLine(terminal, actual, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void FormatStackTrace(ITerminal terminal, FlatException[] exceptions, int index)
    {
        string? stackTrace = GetStringFromIndexOrDefault(exceptions, e => e.StackTrace, index);
        if (RoslynString.IsNullOrWhiteSpace(stackTrace))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);

        string[] lines = stackTrace.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            AppendStackFrame(terminal, line);
        }

        terminal.ResetColor();
    }

    private static void FormatStandardAndErrorOutput(ITerminal terminal, string? standardOutput, string? standardError)
    {
        if (RoslynString.IsNullOrWhiteSpace(standardOutput) && RoslynString.IsNullOrWhiteSpace(standardError))
        {
            return;
        }

        terminal.SetColor(TerminalColor.DarkGray);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.StandardOutput);
        string? standardOutputWithoutSpecialChars = NormalizeSpecialCharacters(standardOutput);
        AppendIndentedLine(terminal, standardOutputWithoutSpecialChars, DoubleIndentation);
        terminal.Append(SingleIndentation);
        terminal.AppendLine(PlatformResources.StandardError);
        string? standardErrorWithoutSpecialChars = NormalizeSpecialCharacters(standardError);
        AppendIndentedLine(terminal, standardErrorWithoutSpecialChars, DoubleIndentation);
        terminal.ResetColor();
    }

    private static void AppendAssemblyLinkTargetFrameworkAndArchitecture(ITerminal terminal, string assembly, string? targetFramework, string? architecture)
    {
        terminal.AppendLink(assembly, lineNumber: null);
        if (targetFramework == null && architecture == null)
        {
            return;
        }

        terminal.Append(" (");
        if (targetFramework != null)
        {
            terminal.Append(targetFramework);
            terminal.Append('|');
        }

        if (architecture != null)
        {
            terminal.Append(architecture);
        }

        terminal.Append(')');
    }

    internal /* for testing */ static void AppendStackFrame(ITerminal terminal, string stackTraceLine)
    {
        terminal.Append(DoubleIndentation);
        Match match = GetFrameRegex().Match(stackTraceLine);
        if (!match.Success)
        {
            terminal.AppendLine(stackTraceLine);
            return;
        }

        bool weHaveFilePathAndCodeLine = !RoslynString.IsNullOrWhiteSpace(match.Groups["code"].Value);
        terminal.Append(PlatformResources.StackFrameAt);
        terminal.Append(' ');

        if (weHaveFilePathAndCodeLine)
        {
            terminal.Append(match.Groups["code"].Value);
        }
        else
        {
            terminal.Append(match.Groups["code1"].Value);
        }

        if (weHaveFilePathAndCodeLine)
        {
            terminal.Append(' ');
            terminal.Append(PlatformResources.StackFrameIn);
            terminal.Append(' ');
            if (!RoslynString.IsNullOrWhiteSpace(match.Groups["file"].Value))
            {
                int line = int.TryParse(match.Groups["line"].Value, out int value) ? value : 0;
                terminal.AppendLink(match.Groups["file"].Value, line);

                // AppendLink finishes by resetting color
                terminal.SetColor(TerminalColor.DarkGray);
            }
        }

        terminal.AppendLine();
    }

    private static void AppendIndentedLine(ITerminal terminal, string? message, string indent)
    {
        if (RoslynString.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!message.Contains('\n'))
        {
            terminal.Append(indent);
            terminal.AppendLine(message);
            return;
        }

        string[] lines = message.Split(NewLineStrings, StringSplitOptions.None);
        foreach (string line in lines)
        {
            // Here we could check if the messages are longer than then line, and reflow them so a long line is split into multiple
            // and prepended by the respective indentation.
            // But this does not play nicely with ANSI escape codes. And if you
            // run in narrow terminal and then widen it the text does not reflow correctly. And you also have harder time copying
            // values when the assertion message is longer.
            terminal.Append(indent);
            terminal.Append(line);
            terminal.AppendLine();
        }
    }

    internal /* for testing */ void AssemblyRunCompleted(string assembly, string? targetFramework, string? architecture, string? executionId,
        // These parameters are useful only for "remote" runs in dotnet test, where we are reporting on multiple processes.
        // In single process run, like with testing platform .exe we report these via messages, and run exit.
        int? exitCode, string? outputData, string? errorData)
    {
        TestProgressState assemblyRun = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        assemblyRun.Stopwatch.Stop();

        _terminalWithProgress.RemoveWorker(assemblyRun.SlotIndex);

        if (!_isDiscovery && _showAssembly && _showAssemblyStartAndComplete)
        {
            _terminalWithProgress.WriteToTerminal(terminal => AppendAssemblySummary(assemblyRun, terminal));
        }

        if (exitCode is null or 0)
        {
            // Report nothing, we don't want to report on success, because then we will also report on test-discovery etc.
            return;
        }

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            AppendExecutableSummary(terminal, exitCode, outputData, errorData);
            terminal.AppendLine();
        });
    }

    private static void AppendExecutableSummary(ITerminal terminal, int? exitCode, string? outputData, string? errorData)
    {
        terminal.AppendLine();
        terminal.Append(PlatformResources.ExitCode);
        terminal.Append(": ");
        terminal.AppendLine(exitCode?.ToString(CultureInfo.CurrentCulture) ?? "<null>");
        terminal.Append(PlatformResources.StandardOutput);
        terminal.AppendLine(":");
        terminal.AppendLine(RoslynString.IsNullOrWhiteSpace(outputData) ? string.Empty : outputData);
        terminal.Append(PlatformResources.StandardError);
        terminal.AppendLine(":");
        terminal.AppendLine(RoslynString.IsNullOrWhiteSpace(errorData) ? string.Empty : errorData);
    }

    private static string? NormalizeSpecialCharacters(string? text)
        => text?.Replace('\0', '\x2400')
            // escape char
            .Replace('\x001b', '\x241b');

    private static void AppendAssemblySummary(TestProgressState assemblyRun, ITerminal terminal)
    {
        int failedTests = assemblyRun.FailedTests;
        int warnings = 0;

        AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assemblyRun.Assembly, assemblyRun.TargetFramework, assemblyRun.Architecture);
        terminal.Append(' ');
        AppendAssemblyResult(terminal, assemblyRun.FailedTests == 0, failedTests, warnings);
        terminal.Append(' ');
        AppendLongDuration(terminal, assemblyRun.Stopwatch.Elapsed);
    }

    /// <summary>
    /// Appends a long duration in human readable format such as 1h 23m 500ms.
    /// </summary>
    private static void AppendLongDuration(ITerminal terminal, TimeSpan duration, bool wrapInParentheses = true, bool colorize = true)
    {
        if (colorize)
        {
            terminal.SetColor(TerminalColor.DarkGray);
        }

        HumanReadableDurationFormatter.Append(terminal, duration, wrapInParentheses);

        if (colorize)
        {
            terminal.ResetColor();
        }
    }

    public void Dispose() => _terminalWithProgress.Dispose();

    internal /* for testing */ void ArtifactAdded(bool outOfProcess, string? assembly, string? targetFramework, string? architecture, string? executionId, string? testName, string path)
        => _artifacts.Add(new TestRunArtifact(outOfProcess, assembly, targetFramework, architecture, executionId, testName, path));

    /// <summary>
    /// Let the user know that cancellation was triggered.
    /// </summary>
    private void StartCancelling()
    {
        _wasCancelled = true;
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.AppendLine();
            terminal.AppendLine(PlatformResources.CancellingTestSession);
            terminal.AppendLine();
        });
    }

    private void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string? executionId, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        asm.AddError(text);

        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Red);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    private void WriteWarningMessage(string assembly, string? targetFramework, string? architecture, string? executionId, string text, int? padding)
    {
        TestProgressState asm = GetOrAddAssemblyRun(assembly, targetFramework, architecture, executionId);
        asm.AddWarning(text);
        _terminalWithProgress.WriteToTerminal(terminal =>
        {
            terminal.SetColor(TerminalColor.Yellow);
            if (padding == null)
            {
                terminal.AppendLine(text);
            }
            else
            {
                AppendIndentedLine(terminal, text, new string(' ', padding.Value));
            }

            terminal.ResetColor();
        });
    }

    private void WriteErrorMessage(string assembly, string? targetFramework, string? architecture, string? executionId, Exception exception)
        => WriteErrorMessage(assembly, targetFramework, architecture, executionId, exception.ToString(), padding: null);

    private void WriteMessage(string text, SystemConsoleColor? color = null, int? padding = null)
    {
        if (color != null)
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                terminal.SetColor(ToTerminalColor(color.ConsoleColor));
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }

                terminal.ResetColor();
            });
        }
        else
        {
            _terminalWithProgress.WriteToTerminal(terminal =>
            {
                if (padding == null)
                {
                    terminal.AppendLine(text);
                }
                else
                {
                    AppendIndentedLine(terminal, text, new string(' ', padding.Value));
                }
            });
        }
    }

    internal void TestDiscovered(
        string assembly,
        string? targetFramework,
        string? architecture,
        string? executionId,
        string? displayName,
        string? uid)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        // TODO: add mode for discovered tests to the progress bar - jajares
        asm.PassedTests++;
        asm.TotalTests++;
        asm.DiscoveredTests.Add(new(displayName, uid));
        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }

    private void AppendTestDiscoverySummary(ITerminal terminal)
    {
        terminal.AppendLine();

        var assemblies = _assemblies.Select(asm => asm.Value).OrderBy(a => a.Assembly).Where(a => a is not null).ToList();

        int totalTests = _assemblies.Values.Sum(a => a.TotalTests);
        bool runFailed = _wasCancelled;

        foreach (TestProgressState assembly in assemblies)
        {
            terminal.Append(string.Format(CultureInfo.CurrentCulture, PlatformResources.DiscoveredTestsInAssembly, assembly.DiscoveredTests.Count));
            terminal.Append(" - ");
            AppendAssemblyLinkTargetFrameworkAndArchitecture(terminal, assembly.Assembly, assembly.TargetFramework, assembly.Architecture);
            terminal.AppendLine();
            foreach ((string? displayName, string? uid) in assembly.DiscoveredTests)
            {
                if (displayName is not null)
                {
                    terminal.Append(SingleIndentation);
                    terminal.AppendLine(displayName);
                }
            }

            terminal.AppendLine();
        }

        terminal.SetColor(runFailed ? TerminalColor.Red : TerminalColor.Green);
        if (assemblies.Count <= 1)
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, PlatformResources.TestDiscoverySummarySingular, totalTests));
        }
        else
        {
            terminal.AppendLine(string.Format(CultureInfo.CurrentCulture, PlatformResources.TestDiscoverySummary, totalTests, assemblies.Count));
        }

        terminal.ResetColor();
        terminal.AppendLine();

        if (_wasCancelled)
        {
            terminal.Append(PlatformResources.Aborted);
            terminal.AppendLine();
        }
    }

    private static TerminalColor ToTerminalColor(ConsoleColor consoleColor)
        => consoleColor switch
        {
            ConsoleColor.Black => TerminalColor.Black,
            ConsoleColor.DarkBlue => TerminalColor.DarkBlue,
            ConsoleColor.DarkGreen => TerminalColor.DarkGreen,
            ConsoleColor.DarkCyan => TerminalColor.DarkCyan,
            ConsoleColor.DarkRed => TerminalColor.DarkRed,
            ConsoleColor.DarkMagenta => TerminalColor.DarkMagenta,
            ConsoleColor.DarkYellow => TerminalColor.DarkYellow,
            ConsoleColor.DarkGray => TerminalColor.DarkGray,
            ConsoleColor.Gray => TerminalColor.Gray,
            ConsoleColor.Blue => TerminalColor.Blue,
            ConsoleColor.Green => TerminalColor.Green,
            ConsoleColor.Cyan => TerminalColor.Cyan,
            ConsoleColor.Red => TerminalColor.Red,
            ConsoleColor.Magenta => TerminalColor.Magenta,
            ConsoleColor.Yellow => TerminalColor.Yellow,
            ConsoleColor.White => TerminalColor.White,
            _ => TerminalColor.Default,
        };

    internal /* for testing */ void TestInProgress(string assembly, string? targetFramework, string? architecture, string testNodeUid, string displayName,
        string? executionId)
    {
        TestProgressState asm = _assemblies[$"{assembly}|{targetFramework}|{architecture}|{executionId}"];

        if (_showActiveTests)
        {
            asm.TestNodeResultsState ??= new(Interlocked.Increment(ref _counter));
            asm.TestNodeResultsState.AddRunningTestNode(
                Interlocked.Increment(ref _counter), testNodeUid, displayName, _createStopwatch());
        }

        _terminalWithProgress.UpdateWorker(asm.SlotIndex);
    }
}
