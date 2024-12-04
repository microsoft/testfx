// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed class TestHostControllersTestHost : CommonTestHost, ITestHost, IDisposable, IOutputDeviceDataProducer
{
    private readonly TestHostControllerConfiguration _testHostsInformation;
    private readonly PassiveNode? _passiveNode;
    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<TestHostControllersTestHost> _logger;
    private readonly ManualResetEventSlim _waitForPid = new(false);

    private bool _testHostGracefullyClosed;
    private int? _testHostExitCode;
    private int? _testHostPID;

    public TestHostControllersTestHost(TestHostControllerConfiguration testHostsInformation, ServiceProvider serviceProvider, PassiveNode? passiveNode, IEnvironment environment,
        ILoggerFactory loggerFactory, IClock clock)
        : base(serviceProvider)
    {
        _testHostsInformation = testHostsInformation;
        _passiveNode = passiveNode;
        _environment = environment;
        _clock = clock;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<TestHostControllersTestHost>();
    }

    public string Uid => nameof(TestHostControllersTestHost);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    protected override bool RunTestApplicationLifeCycleCallbacks => false;

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    protected override async Task<int> InternalRunAsync()
    {
        int exitCode;
        CancellationToken abortRun = ServiceProvider.GetTestApplicationCancellationTokenSource().CancellationToken;
        DateTimeOffset consoleRunStart = _clock.UtcNow;
        var consoleRunStarted = Stopwatch.StartNew();
        IEnvironment environment = ServiceProvider.GetEnvironment();
        IProcessHandler process = ServiceProvider.GetProcessHandler();
        ITestApplicationModuleInfo testApplicationModuleInfo = ServiceProvider.GetTestApplicationModuleInfo();
        ExecutableInfo executableInfo = testApplicationModuleInfo.GetCurrentExecutableInfo();
        ITelemetryCollector telemetry = ServiceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = ServiceProvider.GetTelemetryInformation();
        string? extensionInformation = null;
        var outputDevice = (ProxyOutputDevice)ServiceProvider.GetOutputDevice();
        IConfiguration configuration = ServiceProvider.GetConfiguration();
        try
        {
            using IProcess currentProcess = process.GetCurrentProcess();
            int currentPID = currentProcess.Id;
            string processIdString = currentPID.ToString(CultureInfo.InvariantCulture);
            List<string> partialCommandLine =
            [
                .. executableInfo.Arguments,
                $"--{PlatformCommandLineProvider.TestHostControllerPIDOptionKey}",
                processIdString
            ];

            // Prepare the environment variables used by the test host
            string processCorrelationId = Guid.NewGuid().ToString("N");
            await _logger.LogDebugAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{currentPID} '{processCorrelationId}'");

            NamedPipeServer testHostControllerIpc = new(
                $"MONITORTOHOST_{Guid.NewGuid():N}",
                HandleRequestAsync,
                _environment,
                _loggerFactory.CreateLogger<NamedPipeServer>(),
                ServiceProvider.GetTask(), abortRun);
            testHostControllerIpc.RegisterAllSerializers();

#if NET8_0_OR_GREATER
            IEnumerable<string> arguments = partialCommandLine;
#else
            string arguments = string.Join(" ", partialCommandLine);
#endif
            ProcessStartInfo processStartInfo = new(
                executableInfo.FileName,
                arguments)
            {
                EnvironmentVariables =
                {
                    { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_CORRELATIONID}_{currentPID}", processCorrelationId },
                    { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PARENTPID}_{currentPID}", processIdString },
                    { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{currentPID}", "1" },
                    { $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{currentPID}", testHostControllerIpc.PipeName.Name },
                },
#if !NETCOREAPP
                UseShellExecute = false,
#endif
            };

            List<IDataConsumer> dataConsumersBuilder = [.. _testHostsInformation.DataConsumer];

            // We add the IPlatformOutputDevice after all users extensions.
            IPlatformOutputDevice? display = ServiceProvider.GetServiceInternal<IPlatformOutputDevice>();
            if (display is IDataConsumer dataConsumerDisplay)
            {
                dataConsumersBuilder.Add(dataConsumerDisplay);
            }

            // We register the DotnetTestDataConsumer as last to ensure that it will be the last one to consume the data.
            IPushOnlyProtocol? pushOnlyProtocol = ServiceProvider.GetService<IPushOnlyProtocol>();
            if (pushOnlyProtocol?.IsServerMode == true)
            {
                dataConsumersBuilder.Add(await pushOnlyProtocol.GetDataConsumerAsync());
            }

            // If we're in server mode jsonrpc we add as last consumer the PassiveNodeDataConsumer for the attachments.
            // Connect the passive node if it's available
            if (_passiveNode is not null)
            {
                if (await _passiveNode.ConnectAsync())
                {
                    dataConsumersBuilder.Add(new PassiveNodeDataConsumer(_passiveNode));
                }
                else
                {
                    await _logger.LogWarningAsync("PassiveNode was expected to connect but failed");
                }
            }

            AsynchronousMessageBus concreteMessageBusService = new(
                dataConsumersBuilder.ToArray(),
                ServiceProvider.GetTestApplicationCancellationTokenSource(),
                ServiceProvider.GetTask(),
                ServiceProvider.GetLoggerFactory(),
                ServiceProvider.GetEnvironment());
            await concreteMessageBusService.InitAsync();
            ((MessageBusProxy)ServiceProvider.GetMessageBus()).SetBuiltMessageBus(concreteMessageBusService);

            // Apply the ITestHostEnvironmentVariableProvider
            if (_testHostsInformation.EnvironmentVariableProviders.Length > 0)
            {
                SystemEnvironmentVariableProvider systemEnvironmentVariableProvider = new(environment);
                EnvironmentVariables environmentVariables = new(_loggerFactory)
                {
                    CurrentProvider = systemEnvironmentVariableProvider,
                };
                await systemEnvironmentVariableProvider.UpdateAsync(environmentVariables);

                foreach (ITestHostEnvironmentVariableProvider environmentVariableProvider in _testHostsInformation.EnvironmentVariableProviders)
                {
                    environmentVariables.CurrentProvider = environmentVariableProvider;
                    await environmentVariableProvider.UpdateAsync(environmentVariables);
                }

                environmentVariables.CurrentProvider = null;

                List<(IExtension, string)> failedValidations = [];
                foreach (ITestHostEnvironmentVariableProvider hostEnvironmentVariableProvider in _testHostsInformation.EnvironmentVariableProviders)
                {
                    ValidationResult variableResult = await hostEnvironmentVariableProvider.ValidateTestHostEnvironmentVariablesAsync(environmentVariables);
                    if (!variableResult.IsValid)
                    {
                        failedValidations.Add((hostEnvironmentVariableProvider, variableResult.ErrorMessage));
                    }
                }

                if (failedValidations.Count > 0)
                {
                    StringBuilder displayErrorMessageBuilder = new();
                    StringBuilder logErrorMessageBuilder = new();
                    displayErrorMessageBuilder.AppendLine(PlatformResources.GlobalValidationOfTestHostEnvironmentVariablesFailedErrorMessage);
                    logErrorMessageBuilder.AppendLine("The following 'ITestHostEnvironmentVariableProvider' providers rejected the final environment variables setup:");
                    foreach ((IExtension extension, string errorMessage) in failedValidations)
                    {
                        displayErrorMessageBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.EnvironmentVariableProviderFailedWithError, extension.DisplayName, extension.Uid, errorMessage));
                        displayErrorMessageBuilder.AppendLine(CultureInfo.InvariantCulture, $"Provider '{extension.DisplayName}' (UID: {extension.Uid}) failed with error: {errorMessage}");
                    }

                    await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(displayErrorMessageBuilder.ToString()));
                    await _logger.LogErrorAsync(logErrorMessageBuilder.ToString());
                    return ExitCodes.InvalidPlatformSetup;
                }

                foreach (EnvironmentVariable envVar in environmentVariables.GetAll())
                {
                    processStartInfo.EnvironmentVariables[envVar.Variable] = envVar.Value;
                }
            }

            // Apply the ITestHostProcessLifetimeHandler.BeforeTestHostProcessStartAsync
            if (_testHostsInformation.LifetimeHandlers.Length > 0)
            {
                foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
                {
                    await lifetimeHandler.BeforeTestHostProcessStartAsync(abortRun);
                }
            }

            // Launch the test host process
            string testHostProcessStartupTime = _clock.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            processStartInfo.EnvironmentVariables.Add($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{currentPID}", testHostProcessStartupTime);
            await _logger.LogDebugAsync($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_TESTHOSTPROCESSSTARTTIME}_{currentPID} '{testHostProcessStartupTime}'");
            await _logger.LogDebugAsync($"Starting test host process");
            using IProcess testHostProcess = process.Start(processStartInfo);

            int? testHostProcessId = null;
            try
            {
                testHostProcessId = testHostProcess.Id;
            }
            catch (InvalidOperationException) when (testHostProcess.HasExited)
            {
                // Access PID can throw InvalidOperationException if the process has already exited:
                // System.InvalidOperationException: No process is associated with this object.
            }

            testHostProcess.Exited += (_, _) =>
                _logger.LogDebug($"Test host process exited, PID: '{testHostProcessId}'");

            await _logger.LogDebugAsync($"Started test host process '{testHostProcessId}' HasExited: {testHostProcess.HasExited}");

            string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds];
            int timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : int.Parse(seconds, CultureInfo.InvariantCulture);
            await _logger.LogDebugAsync($"Setting PlatformTestHostControllersManagerSingleConnectionNamedPipeServerWaitConnectionTimeoutSeconds '{timeoutSeconds}'");

            // Wait for the test host controller to connect
            using (CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds)))
            using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, abortRun))
            {
                await _logger.LogDebugAsync($"Wait connection from the test host process");
                await testHostControllerIpc.WaitConnectionAsync(linkedToken.Token);
            }

            // Wait for the test host controller to send the PID of the test host process
            using (CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                _waitForPid.Wait(timeout.Token);
            }

            await _logger.LogDebugAsync($"Fire OnTestHostProcessStartedAsync");

            if (_testHostPID is null)
            {
                throw ApplicationStateGuard.Unreachable();
            }

            if (_testHostsInformation.LifetimeHandlers.Length > 0)
            {
                // We don't block the host during the 'OnTestHostProcessStartedAsync' by-design, if 'ITestHostProcessLifetimeHandler' extensions needs
                // to block the execution of the test host should add an in-process extension like an 'ITestApplicationLifecycleCallbacks' and
                // wait for a connection/signal to return.
                TestHostProcessInformation testHostProcessInformation = new(_testHostPID.Value);
                foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
                {
                    await lifetimeHandler.OnTestHostProcessStartedAsync(testHostProcessInformation, abortRun);
                }
            }

            await _logger.LogDebugAsync($"Wait for test host process exit");
            await testHostProcess.WaitForExitAsync();

            if (_testHostsInformation.LifetimeHandlers.Length > 0)
            {
                await _logger.LogDebugAsync($"Fire OnTestHostProcessExitedAsync testHostGracefullyClosed: {_testHostGracefullyClosed}");
                var messageBusProxy = (MessageBusProxy)ServiceProvider.GetMessageBus();

                ApplicationStateGuard.Ensure(_testHostPID is not null);
                TestHostProcessInformation testHostProcessInformation = new(_testHostPID.Value, testHostProcess.ExitCode, _testHostGracefullyClosed);
                foreach (ITestHostProcessLifetimeHandler lifetimeHandler in _testHostsInformation.LifetimeHandlers)
                {
                    await lifetimeHandler.OnTestHostProcessExitedAsync(testHostProcessInformation, abortRun);

                    // OnTestHostProcess could produce information that needs to be handled by others.
                    await messageBusProxy.DrainDataAsync();
                }

                // We disable after the drain because it's possible that the drain will produce more messages
                await messageBusProxy.DrainDataAsync();
                await messageBusProxy.DisableAsync();
            }

            await outputDevice.DisplayAfterSessionEndRunAsync();

            // We collect info about the extensions before the dispose to avoid possible issue with cleanup.
            if (telemetryInformation.IsEnabled)
            {
                extensionInformation = await ExtensionInformationCollector.CollectAndSerializeToJsonAsync(ServiceProvider);
            }

            // If we have a process in the middle between the test host controller and the test host process we need to keep it into account.
            exitCode = _testHostExitCode ??
                (abortRun.IsCancellationRequested
                    ? ExitCodes.TestSessionAborted
                    : (!_testHostGracefullyClosed ? ExitCodes.TestHostProcessExitedNonGracefully : throw ApplicationStateGuard.Unreachable()));

            if (!_testHostGracefullyClosed && !abortRun.IsCancellationRequested)
            {
                await outputDevice.DisplayAsync(this, new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.TestProcessDidNotExitGracefullyErrorMessage, exitCode)));
            }

            await _logger.LogInformationAsync($"TestHostControllersTestHost ended with exit code '{exitCode}' (real test host exit code '{testHostProcess.ExitCode}')' in '{consoleRunStarted.Elapsed}'");
            await DisposeHelper.DisposeAsync(testHostControllerIpc);
        }
        finally
        {
            await DisposeServicesAsync();
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
                [TelemetryProperties.HostProperties.HasExitedGracefullyPropertyName] = _testHostGracefullyClosed.AsTelemetryBool(),
                [TelemetryProperties.HostProperties.ExtensionsPropertyName] = extensionInformation,
            });
        }

        return exitCode;
    }

    private async Task DisposeServicesAsync()
    {
        ITestHostEnvironmentVariableProvider[] variableProviders = _testHostsInformation.EnvironmentVariableProviders;
        ITestHostProcessLifetimeHandler[] lifetimeHandlers = _testHostsInformation.LifetimeHandlers;

        List<object> alreadyDisposed = new(lifetimeHandlers.Length + variableProviders.Length);

        foreach (ITestHostProcessLifetimeHandler service in lifetimeHandlers)
        {
            await DisposeHelper.DisposeAsync(service);
            alreadyDisposed.Add(service);
        }

        foreach (ITestHostEnvironmentVariableProvider service in variableProviders)
        {
            await DisposeHelper.DisposeAsync(service);
            alreadyDisposed.Add(service);
        }

        await DisposeServiceProviderAsync(ServiceProvider, alreadyDisposed: alreadyDisposed);
    }

    private Task<IResponse> HandleRequestAsync(IRequest request)
    {
        try
        {
            switch (request)
            {
                case TestHostProcessExitRequest testHostProcessExitRequest:
                    _testHostExitCode = testHostProcessExitRequest.ExitCode;
                    _testHostGracefullyClosed = true;
                    return Task.FromResult<IResponse>(VoidResponse.CachedInstance);

                case TestHostProcessPIDRequest testHostProcessPIDRequest:
                    _testHostPID = testHostProcessPIDRequest.PID;
                    _waitForPid.Set();
                    return Task.FromResult<IResponse>(VoidResponse.CachedInstance);

                default:
                    throw new NotSupportedException($"Request '{request}' not supported");
            }
        }
        catch (Exception ex)
        {
            _environment.FailFast($"[TestHostControllersTestHost] Unhandled exception:\n{ex}", ex);
            throw;
        }
    }

    public void Dispose()
        => _waitForPid.Dispose();
}
