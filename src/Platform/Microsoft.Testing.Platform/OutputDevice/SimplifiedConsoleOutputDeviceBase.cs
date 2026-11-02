// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Base class for browser and WASI output devices that provides common functionality.
/// </summary>
internal abstract partial class SimplifiedConsoleOutputDeviceBase : IPlatformOutputDevice,
    IDataConsumer,
    IOutputDeviceDataProducer,
    ITestSessionLifetimeHandler,
    IAsyncInitializableExtension
{
    private readonly IConsole _console;
    private readonly IAsyncMonitor _asyncMonitor;
    private readonly IRuntimeFeature _runtimeFeature;
    private readonly IEnvironment _environment;
    private readonly IPlatformInformation _platformInformation;
    private readonly IStopPoliciesService _policiesService;
    private readonly ActiveTestTracker _activeTestTracker;
    private readonly TimeSpan _slowTestPollInterval;
    private readonly string? _longArchitecture;
    private readonly Dictionary<ProgressMessageIdentity, string> _progressMessages = [];

    // The effective runtime that is executing the application e.g. .NET 9, when .NET 8 application is running with --roll-forward latest.
    private readonly string? _runtimeFramework;

    // The targeted framework, .NET 8 when application specifies <TargetFramework>net8.0</TargetFramework>
    private readonly string? _targetFramework;
    private readonly string _assemblyName;

    // Browser and WASI hosts run one test session per application. The engine calls this handler twice for that
    // session because the output device implements two extension roles, so only the first callback starts the reporter.
    private bool _firstCallTo_OnSessionStartingAsync = true;
    private bool _bannerDisplayed;
    private volatile bool _wasCancelled;
    private SlowTestReporterState? _slowTestReporterState;

    private int _passedTests;
    private int _failedTests;
    private int _skippedTests;

    protected SimplifiedConsoleOutputDeviceBase(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        IStopPoliciesService policiesService)
        : this(
            console,
            testApplicationModuleInfo,
            asyncMonitor,
            runtimeFeature,
            environment,
            platformInformation,
            policiesService,
            ProgressReportingConfiguration.GetThreshold(
                environment, ProgressReportingConfiguration.MTP_PROGRESS_SLOW_TEST_SECONDS, defaultSeconds: 60),
            SystemStopwatch.StartNew,
            TimeSpan.FromSeconds(1))
    {
    }

    internal SimplifiedConsoleOutputDeviceBase(
        IConsole console,
        ITestApplicationModuleInfo testApplicationModuleInfo, IAsyncMonitor asyncMonitor,
        IRuntimeFeature runtimeFeature, IEnvironment environment, IPlatformInformation platformInformation,
        IStopPoliciesService policiesService, TimeSpan slowTestThreshold,
        Func<IStopwatch> createStopwatch, TimeSpan slowTestPollInterval)
    {
        _console = console;
        _asyncMonitor = asyncMonitor;
        _runtimeFeature = runtimeFeature;
        _environment = environment;
        _platformInformation = platformInformation;
        _policiesService = policiesService;
        _activeTestTracker = new(slowTestThreshold, createStopwatch);
        _slowTestPollInterval = slowTestPollInterval;

        if (_runtimeFeature.IsDynamicCodeSupported)
        {
#if !NETCOREAPP
            _longArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#else
            // RID has the operating system, we want to see that in the banner, but not next to every dll.
            _longArchitecture = RuntimeInformation.RuntimeIdentifier;
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

    /// <inheritdoc />
    public string Uid => GetType().Name;

    /// <inheritdoc />
    public string Version => PlatformVersion.Version;

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    protected virtual bool DisplayActiveTestProgress => false;

    public async Task InitializeAsync()
        => await _policiesService.RegisterOnAbortCallbackAsync(
            () =>
            {
                _wasCancelled = true;
                ConsoleLog(PlatformResources.CancellingTestSession);
                ConsoleLog(PlatformResources.PressCtrlCAgainToForceExit);
                return Task.CompletedTask;
            }).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    protected abstract void ConsoleWarn(string? message);

    protected abstract void ConsoleError(string? message);

    protected abstract void ConsoleLog(string? message);

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
