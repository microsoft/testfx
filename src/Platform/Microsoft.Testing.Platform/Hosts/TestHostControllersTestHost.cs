// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

[UnsupportedOSPlatform("browser")]
[StackTraceHidden]
internal sealed partial class TestHostControllersTestHost : CommonHost, IHost, IDisposable, IOutputDeviceDataProducer
{
    private static readonly TimeSpan TestHostTerminationTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _testHostCooperativeShutdownTimeout;
    private readonly TimeSpan _controllerExtensionFinalizationTimeout;
    private readonly TestHostControllerConfiguration _testHostsInformation;
    private readonly PassiveNode? _passiveNode;
    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TestHostControllersTestHost> _logger;
    private readonly ManualResetEventSlim _waitForPid = new(false);
    private readonly List<object> _servicesStillRunning = [];

    // This flag means that the testhost was able to correctly complete in the child process.
    // But it doesn't mean we will exit successfully.
    // For example, the test session might complete successfully but leaves a hanging foreground thread.
    // In that case, hang dump might kick in and kill the hanging process.
    private bool _testHostCompletedReceived;

    // This is the exit code received from the test host process via IPC. It might not be the same as the actual exit code.
    private int? _testHostExitCodeReceived;
    private int? _testHostUnfilteredExitCodeReceived;

    private int? _testHostPID;
    private bool _controllerFinalizationTimedOut;
    private CancellationTokenSource? _controllerFinalizationCancellationTokenSource;
    private CancellationTokenRegistration _controllerFinalizationTransitionRegistration;
    private int _controllerFinalizationTimeoutArmed;
    private int _finalizationTimeoutWarningScheduled;

    public TestHostControllersTestHost(TestHostControllerConfiguration testHostsInformation, ServiceProvider serviceProvider, PassiveNode? passiveNode, IEnvironment environment,
        ILoggerFactory loggerFactory, IClock clock)
        : base(serviceProvider)
    {
        _testHostsInformation = testHostsInformation;
        _passiveNode = passiveNode;
        _environment = environment;
        _testHostCooperativeShutdownTimeout =
            ShutdownTimeouts.GetCanceledConsumerCompletion(environment) + TimeSpan.FromSeconds(15);
        _controllerExtensionFinalizationTimeout = ShutdownTimeouts.GetControllerFinalization(environment);
        _clock = clock;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<TestHostControllersTestHost>();
    }

    public string Uid => nameof(TestHostControllersTestHost);

    public string Version => PlatformVersion.Version;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    protected override bool RunTestApplicationLifeCycleCallbacks => false;

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    protected override async Task<int> InternalRunAsync(CancellationToken cancellationToken, List<object> alreadyDisposed)
    {
        int exitCode;
        TestHostProcessInformation testHostProcessInformation;
        DateTimeOffset consoleRunStart = _clock.UtcNow;
        var consoleRunStarted = Stopwatch.StartNew();
        IEnvironment environment = ServiceProvider.GetEnvironment();
        IProcessHandler process = ServiceProvider.GetProcessHandler();
        ITestApplicationModuleInfo testApplicationModuleInfo = ServiceProvider.GetTestApplicationModuleInfo();
        ITelemetryCollector telemetry = ServiceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = ServiceProvider.GetTelemetryInformation();
        string? extensionInformation = null;
        var outputDevice = (ProxyOutputDevice)ServiceProvider.GetOutputDevice();
        IConfiguration configuration = ServiceProvider.GetConfiguration();
        NamedPipeServer? testHostControllerIpc = null;
        TestHostControllerCancellationServer? testHostControllerCancellationServer = null;
        using CancellationTokenSource testHostControllerIpcLifetime = new();
        try
        {
            int currentPid = environment.ProcessId;
            string processIdString = currentPid.ToString(CultureInfo.InvariantCulture);

            ExecutableInfo executableInfo = testApplicationModuleInfo.GetCurrentExecutableInfo();
            await _logger.LogDebugAsync($"Test host controller process info: {executableInfo}").ConfigureAwait(false);

            string processCorrelationId = Guid.NewGuid().ToString("N");
            await _logger.LogDebugAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{currentPid} '{processCorrelationId}'").ConfigureAwait(false);

            IReadOnlyList<string>? authorizedSecurityIdentities = await ServiceProvider.ResolveTestHostControllerAuthorizedSecurityIdentitiesAsync(
                _testHostsInformation.TestHostLauncher,
                executableInfo.FilePath,
                _logger,
                cancellationToken).ConfigureAwait(false);
            testHostControllerIpc = CreateTestHostControllerIpc(authorizedSecurityIdentities, testHostControllerIpcLifetime.Token);
            testHostControllerCancellationServer = new(
                authorizedSecurityIdentities,
                environment,
                _loggerFactory,
                ServiceProvider.GetTask());
            testHostControllerCancellationServer.Start();

            (ProcessStartInfo ProcessStartInfo, IReadOnlyList<string> PartialCommandLine)? processConfiguration =
                await PrepareProcessConfigurationAsync(
                    executableInfo,
                    currentPid,
                    processIdString,
                    processCorrelationId,
                    testHostControllerIpc,
                    testHostControllerCancellationServer,
                    environment,
                    outputDevice,
                    cancellationToken).ConfigureAwait(false);
            if (processConfiguration is null)
            {
                return (int)ExitCode.InvalidPlatformSetup;
            }

            (exitCode, testHostProcessInformation, extensionInformation) =
                await RunTestHostProcessAsync(
                    processConfiguration.Value.ProcessStartInfo,
                    processConfiguration.Value.PartialCommandLine,
                    currentPid,
                    process,
                    configuration,
                    testHostControllerIpc,
                    testHostControllerCancellationServer,
                    outputDevice,
                    telemetryInformation,
                    consoleRunStarted,
                    cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                try
                {
                    if (testHostControllerCancellationServer is not null)
                    {
                        await DisposeHelper.DisposeAsync(testHostControllerCancellationServer).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (testHostControllerIpc is not null)
                    {
#if NET
                        await testHostControllerIpcLifetime.CancelAsync().ConfigureAwait(false);
#else
                        testHostControllerIpcLifetime.Cancel();
#endif
                        await DisposeHelper.DisposeAsync(testHostControllerIpc).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                // Service disposal must still run if closing the connection reports a failure.
                await DisposeServicesAsync(alreadyDisposed).ConfigureAwait(false);
            }
        }

        if (telemetryInformation.IsEnabled)
        {
            ApplicationStateGuard.Ensure(extensionInformation is not null);
            DateTimeOffset consoleRunStop = _clock.UtcNow;
            await telemetry.LogEventAsync(TelemetryEvents.TestHostControllersTestHostExitEventName, new Dictionary<string, object>
            {
                [TelemetryProperties.HostProperties.RunStart] = consoleRunStart,
                [TelemetryProperties.HostProperties.RunStop] = consoleRunStop,
                [TelemetryProperties.HostProperties.ExitCodePropertyName] = exitCode.ToString(CultureInfo.InvariantCulture),
                [TelemetryProperties.HostProperties.HasExitedGracefullyPropertyName] = testHostProcessInformation.HasExitedGracefully.AsTelemetryBool(),
                [TelemetryProperties.HostProperties.ExtensionsPropertyName] = extensionInformation,
            }, cancellationToken).ConfigureAwait(false);
        }

        return exitCode;
    }
}
