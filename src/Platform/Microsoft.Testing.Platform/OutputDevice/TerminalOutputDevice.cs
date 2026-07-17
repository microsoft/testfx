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
    private readonly Dictionary<ProgressMessageIdentity, string> _jsonProgressMessages = [];
#if NET9_0_OR_GREATER
    private readonly Lock _jsonProgressMessagesLock = new();
#else
    private readonly object _jsonProgressMessagesLock = new();
#endif

    private TerminalTestReporter? _terminalTestReporter;
    private bool _bannerDisplayed;
    private bool _isListTests;
    private bool _isListTestsJson;
    private bool _isServerMode;
    private bool _isAzureDevOpsEnvironment;
    private ILogger? _logger;
    private TestProcessRole? _processRole;

    private readonly record struct ProgressMessageIdentity(string ProducerUid, string Key);

    private void ClearJsonProgressMessages()
    {
        lock (_jsonProgressMessagesLock)
        {
            _jsonProgressMessages.Clear();
        }
    }

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
