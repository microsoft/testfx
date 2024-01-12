// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Testing.Framework;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform.Hosts;

internal class TestHostBuilder(IFileSystem fileSystem, IRuntimeFeature runtimeFeature, IEnvironment environment, IProcessHandler processHandler, ITestApplicationModuleInfo testApplicationModuleInfo) : ITestHostBuilder
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo = testApplicationModuleInfo;

    public ITestFrameworkManager? TestFramework { get; set; }

    public ITestHostManager TestHost { get; } = new TestHostManager();

    public IConfigurationManager Configuration { get; } = new ConfigurationManager(fileSystem, testApplicationModuleInfo);

    public ILoggingManager Logging { get; } = new LoggingManager();

    public IPlatformOutputDeviceManager OutputDisplay { get; } = new PlatformOutputDeviceManager();

    public ICommandLineManager CommandLine { get; } = new CommandLineManager(runtimeFeature, environment, processHandler, testApplicationModuleInfo);

    public ITelemetryManager Telemetry { get; } = new TelemetryManager();

    public IServerModeManager ServerMode { get; } = new ServerModeManager();

    public ITestHostControllersManager TestHostControllers { get; } = new TestHostControllersManager();

    public IToolsManager Tools { get; } = new ToolsManager();

    public ITestHostOrchestratorManager TestHostOrchestratorManager { get; } = new TestHostOrchestratorManager();

    public async Task<ITestHost> BuildAsync(
        string[] args,
        ApplicationLoggingState loggingState,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        DateTimeOffset createBuilderStart)
    {
        // ============= SETUP COMMON SERVICE USED IN ALL MODES ===============//
        ArgumentGuard.IsNotNull(TestFramework);

        var systemClock = new SystemClock();
        DateTimeOffset buildBuilderStart = systemClock.UtcNow;

        // Note: These metrics should be populated with the builder configuration,
        //       and the overall state of the system.
        var builderMetrics = new Dictionary<string, object>
        {
            [TelemetryProperties.HostProperties.CreateBuilderStart] = createBuilderStart,
            [TelemetryProperties.HostProperties.BuildBuilderStart] = buildBuilderStart,
        };
        var systemEnvironment = new SystemEnvironment();

        // Default service provider bag
        ServiceProvider serviceProvider = new();

        // Add the service provider to the service collection
        if (loggingState.FileLoggerProvider is not null)
        {
            serviceProvider.TryAddService(loggingState.FileLoggerProvider);
        }

        // Add required non-skippable system "services"
        serviceProvider.TryAddService(loggingState);
        serviceProvider.TryAddService(systemEnvironment);
        var systemConsole = new SystemConsole();
        serviceProvider.TryAddService(systemConsole);
        var systemTask = new SystemTask();
        serviceProvider.TryAddService(systemTask);
        serviceProvider.TryAddService(runtimeFeature);
        serviceProvider.TryAddService(processHandler);
        serviceProvider.TryAddService(_fileSystem);
        serviceProvider.TryAddService(_testApplicationModuleInfo);
        TestHostControllerInfo testHostControllerInfo = new(loggingState.CommandLineParseResult);
        serviceProvider.TryAddService(testHostControllerInfo);

        serviceProvider.TryAddService(systemClock);
        var systemMonitor = new SystemMonitor();
        serviceProvider.TryAddService(systemMonitor);
        var systemMonitorAsyncFactory = new SystemMonitorAsyncFactory();
        serviceProvider.TryAddService(systemMonitorAsyncFactory);

        ILogger? logger = null;
        if (loggingState.FileLoggerProvider is not null)
        {
            logger = loggingState.FileLoggerProvider.CreateLogger(GetType().ToString());
        }

        if (logger is not null)
        {
            await logger.LogInformationAsync($"Setting RegisterEnvironmentVariablesConfigurationSource: '{testApplicationOptions.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource}'");
        }

        // By default the env var configuration source is enabled, check if we have to disable it.
        if (testApplicationOptions.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource)
        {
            Configuration.AddConfigurationSource(() => new EnvironmentVariablesConfigurationSource(systemEnvironment));
        }

        // Add the default json configuration source, this will read the standard test platform json configuration file.
        Configuration.AddConfigurationSource(() => new JsonConfigurationSource(_testApplicationModuleInfo, _fileSystem, loggingState.FileLoggerProvider));

        // Build the IConfiguration - we need special treatment because the configuration is needed by extensions.
        var configuration = (AggregatedConfiguration)await ((ConfigurationManager)Configuration).BuildAsync(loggingState.FileLoggerProvider);
        serviceProvider.TryAddService(configuration);

        // Current test platform is picky on unhandled exception, we will tear down the process in that case.
        // This mode can be too aggressive especially compared to the old framework, so we allow the user to disable it if their suite
        // relies on unhandled exception.
        IEnvironment environment = serviceProvider.GetEnvironment();

        // Check the config file, by default is not specified the policy is false.
        _ = bool.TryParse(configuration[PlatformConfigurationConstants.PlatformExitProcessOnUnhandledException]!, out bool isFileConfiguredToFailFast);

        // In VSTest mode we run inside the testhost where we could have expected unhandled exception, so we don't want to fail fast.
        bool isVsTestAdapterMode = loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey);

        // Check the environment variable, it wins on all the other configuration.
        string? environmentSetting = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION);
        bool? isEnvConfiguredToFailFast = null;
        if (environmentSetting is not null and ("1" or "0"))
        {
            isEnvConfiguredToFailFast = environmentSetting == "1";
        }

        // Environment variable has priority over the config file.
        // In both case we don't want to fail fast in VSTest mode.
        bool exitProcessOnUnhandledException = false;

        // If we are in VSTest mode we don't want to fail fast.
        if (!isVsTestAdapterMode)
        {
            // If the environment variable is not set, we check the config file.
            exitProcessOnUnhandledException = isEnvConfiguredToFailFast is not null ? environmentSetting == "1" : isFileConfiguredToFailFast;
        }

        if (logger is not null)
        {
            await logger.LogInformationAsync($"Setting PlatformExitProcessOnUnhandledException: '{exitProcessOnUnhandledException}', config file: {isFileConfiguredToFailFast} environment variable: {isEnvConfiguredToFailFast} VSTest mode: {isVsTestAdapterMode}");
        }

        if (exitProcessOnUnhandledException)
        {
            unhandledExceptionsHandler.Subscribe();
        }

        var unhandledExceptionsPolicy = new UnhandledExceptionsPolicy(fastFailOnFailure: exitProcessOnUnhandledException);
        serviceProvider.AddService(unhandledExceptionsPolicy);

        // Add the lifetime application implementation
        var testApplicationCancellationTokenSource = new CTRLPlusCCancellationTokenSource(
            systemConsole,
            loggingState.FileLoggerProvider?.CreateLogger(nameof(CTRLPlusCCancellationTokenSource)));
        serviceProvider.AddService(testApplicationCancellationTokenSource, throwIfSameInstanceExit: true);

        // Add output display proxy, needed by command line manager.
        // We don't add to the service right now because we need special treatment between console/server mode.
        IPlatformOutputDevice platformOutputDevice = await ((PlatformOutputDeviceManager)OutputDisplay).BuildAsync(serviceProvider, loggingState);

        // Add the platform output device to the service provider for both modes.
        serviceProvider.TryAddService(platformOutputDevice);

        // Build the command line service - we need special treatment because is possible that an extension query it during the creation.
        // Add Retry default argument commandlines
        CommandLineHandler commandLineHandler = await ((CommandLineManager)CommandLine).BuildAsync(args, platformOutputDevice, loggingState.CommandLineParseResult);

        // If command line is not valid we return immediately.
        if (!loggingState.CommandLineParseResult.HasTool && !await commandLineHandler.ParseAndValidateAsync())
        {
            return new InformativeCommandLineTestHost(ExitCodes.InvalidCommandLine);
        }

        // Register as ICommandLineOptions.
        serviceProvider.TryAddService(commandLineHandler);

        // Add FileLoggerProvider if needed
        if (loggingState.FileLoggerProvider is not null)
        {
            Logging.AddProvider((_, _) => loggingState.FileLoggerProvider);
        }

        // Build the command like service.
        ICommandLineOptions commandLineOptions = serviceProvider.GetCommandLineOptions();
        if (commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey))
        {
            Logging.AddProvider((logLevel, services) => new ServerLoggerForwarderProvider(
                logLevel,
                services.GetTask(),
                services.GetService<ServerTestHost>()));
        }

        // Build the logger factory.
        ILoggerFactory loggerFactory = await ((LoggingManager)Logging).BuildAsync(serviceProvider, loggingState.LogLevel, systemMonitor);
        serviceProvider.TryAddService(loggerFactory);

        // At this point we start to build extensions so we need to have all the information complete for the usage,
        // here we ensure to override the result directory if user passed the argument --results-directory in command line.
        // After this check users can get the result directory using IConfiguration["testingPlatform:resultDirectory"] or the
        // extension method helper serviceProvider.GetConfiguration()
        await configuration.CheckTestResultsDirectoryOverrideAndCreateItAsync(commandLineOptions, loggingState.FileLoggerProvider);

        // Display banner now because we need capture the output in case of MSBuild integration and we want to forward
        // to file disc also the banner, so at this point we need to have all services and configuration(result directory) built.
        bool isNoBannerSet = loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.NoBannerOptionKey);
        string? noBannerEnvironmentVar = environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER);
        string? dotnetNoLogoEnvironmentVar = environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_NOLOGO);
        if (!isNoBannerSet && !(noBannerEnvironmentVar is "1" or "true") && !(dotnetNoLogoEnvironmentVar is "1" or "true"))
        {
            await platformOutputDevice.DisplayBannerAsync();
        }

        // Add global telemetry service.
        // Add at this point or the telemetry banner appearance order will be wrong, we want the testing app banner before the telemetry banner.
        ITelemetryCollector telemetryService = await ((TelemetryManager)Telemetry).BuildAsync(serviceProvider, loggerFactory, testApplicationOptions);
        serviceProvider.TryAddService(telemetryService);
        AddApplicationMetadata(serviceProvider, builderMetrics);

        // ============= SETUP COMMON SERVICE USED IN ALL MODES END ===============//

        // ============= SELECT AND RUN THE ACTUAL MODE ===============//

        // ======= TOOLS MODE ======== //
        // Add the platform output device to the service provider.
        var toolsServiceProvider = (ServiceProvider)serviceProvider.Clone();
        toolsServiceProvider.TryAddService(platformOutputDevice);
        ToolsInformation toolsInformation = await ((ToolsManager)Tools).BuildAsync(toolsServiceProvider);
        if (loggingState.CommandLineParseResult.HasTool)
        {
            // Add the platform output device to the service provider.
            serviceProvider.TryAddService(platformOutputDevice);

            var toolsTestHost = new ToolsTestHost(
                toolsInformation,
                serviceProvider,
                loggingState.CommandLineParseResult,
                commandLineHandler.ExtensionsCommandLineOptionsProviders,
                commandLineHandler,
                platformOutputDevice);

            await LogTestHostCreatedAsync(
                serviceProvider,
                mode: TelemetryProperties.ApplicationMode.Tool,
                metrics: builderMetrics,
                stop: systemClock.UtcNow);

            return toolsTestHost;
        }

        // If --help is invoked we return
        if (commandLineHandler.IsHelpInvoked())
        {
            await commandLineHandler.PrintHelpAsync(toolsInformation.Tools);
            return new InformativeCommandLineTestHost(0);
        }

        // If --info is invoked we return
        if (commandLineHandler.IsInfoInvoked())
        {
            await commandLineHandler.PrintInfoAsync(toolsInformation.Tools);
            return new InformativeCommandLineTestHost(0);
        }

        // ======= TEST HOST ORCHESTRATOR ======== //
        TestHostOrchestratorConfiguration testHostOrchestratorConfiguration = await TestHostOrchestratorManager.BuildAsync(serviceProvider);
        if (testHostOrchestratorConfiguration.TestHostOrchestrators.Length > 0)
        {
            return new TestHostOrchestratorHost(testHostOrchestratorConfiguration, serviceProvider);
        }

        // ======= TEST HOST CONTROLLER MODE ======== //
        // Check if we're in the test host or we should check test controllers extensions
        // Environment variable check should not be needed but in case we will rollback to use only env var we will need it.
        if (!testHostControllerInfo.HasTestHostController ||
            systemEnvironment.GetEnvironmentVariable($"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION}_{testHostControllerInfo.GetTestHostControllerPID(true)}") != "1")
        {
            // Clone the service provider to avoid to add the message bus proxy to the main service provider.
            var testHostControllersServiceProvider = (ServiceProvider)serviceProvider.Clone();

            // Add the platform output device to the service provider.
            testHostControllersServiceProvider.TryAddService(platformOutputDevice);

            // Add the message bus proxy specific for the launchers.
            testHostControllersServiceProvider.TryAddService(new MessageBusProxy());
            TestHostControllerConfiguration testHostControllers = await ((TestHostControllersManager)TestHostControllers).BuildAsync(testHostControllersServiceProvider);
            if (testHostControllers.RequireProcessRestart)
            {
                var testHostControllersTestHost = new TestHostControllersTestHost(testHostControllers, testHostControllersServiceProvider, systemEnvironment, loggerFactory, systemClock);

                await LogTestHostCreatedAsync(
                    serviceProvider,
                    mode: TelemetryProperties.ApplicationMode.TestHostControllers,
                    metrics: builderMetrics,
                    stop: systemClock.UtcNow);

                return testHostControllersTestHost;
            }
        }

        // ======= TEST HOST MODE ======== //

        // Setup the test host working folder.
        // Out of the test host controller extension the current working directory is the test host working directory.
        string? currentWorkingDirectory = configuration[PlatformConfigurationConstants.PlatformCurrentWorkingDirectory];
        ArgumentGuard.IsNotNull(currentWorkingDirectory);
        configuration.SetTestHostWorkingDirectory(currentWorkingDirectory);

        // If we're under test controllers and currently we're inside the started test host we connect to the out of process
        // test controller manager.
        NamedPipeClient? testControllerConnection = await ConnectToTestHostProcessMonitorIfAvailableAsync(
            processHandler,
            testApplicationCancellationTokenSource,
            loggerFactory.CreateLogger(nameof(ConnectToTestHostProcessMonitorIfAvailableAsync)),
            testHostControllerInfo,
            configuration,
            systemEnvironment);

        // Build and register the test application lifecycle callbacks.
        ITestApplicationLifecycleCallbacks[] testApplicationLifecycleCallback =
            await ((TestHostManager)TestHost).BuildTestApplicationLifecycleCallbackAsync(serviceProvider);
        serviceProvider.AddServices(testApplicationLifecycleCallback);

        // ServerMode and Console mode uses different host
        if (commandLineHandler.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey))
        {
            // Build the server mode with the user preferences
            IMessageHandlerFactory messageHandlerFactory = ((ServerModeManager)ServerMode).Build(serviceProvider);

            // Build the test host
            // note that we pass the BuildTestFrameworkAsync as callback because server mode will call it per-request
            // this is not needed in console mode where we have only 1 request.
            var serverTestHost =
                new ServerTestHost(serviceProvider, BuildTestFrameworkAsync, messageHandlerFactory, (TestFrameworkManager)TestFramework, (TestHostManager)TestHost);

            // If needed we wrap the host inside the TestHostControlledHost to automatically handle the shutdown of the connected pipe.
            ITestHost? actualTestHost = testControllerConnection is not null
                ? new TestHostControlledHost(testControllerConnection, serverTestHost, testApplicationCancellationTokenSource.CancellationToken)
                : serverTestHost;

            // Log Telemetry
            await LogTestHostCreatedAsync(
                serviceProvider,
                mode: TelemetryProperties.ApplicationMode.Server,
                metrics: builderMetrics,
                stop: systemClock.UtcNow);

            return actualTestHost;
        }
        else
        {
            // Add custom ITestExecutionFilterFactory to the service list if available
            ActionResult<ITestExecutionFilterFactory> testExecutionFilterFactoryResult = await ((TestHostManager)TestHost).TryBuildTestExecutionFilterFactoryAsync(serviceProvider);
            if (testExecutionFilterFactoryResult.IsSuccess)
            {
                serviceProvider.TryAddService(testExecutionFilterFactoryResult.Result);
            }

            // Add custom ITestExecutionFilterFactory to the service list if available
            ActionResult<ITestFrameworkInvoker> testAdapterInvokerBuilderResult = await ((TestHostManager)TestHost).TryBuildTestAdapterInvokerAsync(serviceProvider);
            if (testAdapterInvokerBuilderResult.IsSuccess)
            {
                serviceProvider.TryAddService(testAdapterInvokerBuilderResult.Result);
            }

            // Override hot reload telemetry if needed
            ITelemetryInformation telemetryInformation = serviceProvider.GetTelemetryInformation();
            if (telemetryInformation.IsEnabled)
            {
                builderMetrics[TelemetryProperties.HostProperties.IsHotReloadPropertyName] = serviceProvider.GetRuntimeFeature().IsHotReloadEnabled.AsTelemetryBool();
            }

            // Build the test host
            ConsoleTestHost consoleHost = CreateConsoleTestHost(
                serviceProvider,
                BuildTestFrameworkAsync,
                (TestFrameworkManager)TestFramework,
                (TestHostManager)TestHost);

            // If needed we wrap the host inside the TestHostControlledHost to automatically handle the shutdown of the connected pipe.
            ITestHost? actualTestHost = testControllerConnection is not null
                ? new TestHostControlledHost(testControllerConnection, consoleHost, testApplicationCancellationTokenSource.CancellationToken)
                : consoleHost;

            // Log Telemetry
#pragma warning disable SA1118 // Parameter should not span multiple lines
            await LogTestHostCreatedAsync(
                serviceProvider,
                mode: loggingState.CommandLineParseResult.IsOptionSet(PlatformCommandLineProvider.VSTestAdapterModeOptionKey)
                    ? TelemetryProperties.ApplicationMode.VSTestAdapterMode
                    : TelemetryProperties.ApplicationMode.Console,
                metrics: builderMetrics,
                stop: systemClock.UtcNow);
#pragma warning restore SA1118 // Parameter should not span multiple lines

            return actualTestHost;
        }
    }

    private static async Task<NamedPipeClient?> ConnectToTestHostProcessMonitorIfAvailableAsync(
        IProcessHandler processHandler,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource,
        ILogger logger,
        TestHostControllerInfo testHostControllerInfo,
        IConfiguration configuration,
        SystemEnvironment environment)
    {
        if (!testHostControllerInfo.HasTestHostController)
        {
            return null;
        }

        string pipeEnvironmentVariable = $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{testHostControllerInfo.GetTestHostControllerPID(true)}";
        string? pipeName = environment.GetEnvironmentVariable(pipeEnvironmentVariable) ?? throw new InvalidOperationException($"Unexpected null pipe name from environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}'");

        // RemoveVariable the environment variable so that it doesn't get passed to the eventually children processes
        environment.SetEnvironmentVariable(pipeEnvironmentVariable, string.Empty);

        // Create client to connect to the monitor
        var client = new NamedPipeClient(pipeName);
        client.RegisterAllSerializers();

        // Connect to the monitor
        await logger.LogDebugAsync($"Connecting to named pipe '{pipeName}'");
        string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds];

        // Default timeout is 30 seconds
        int timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : int.Parse(seconds, CultureInfo.InvariantCulture);
        await logger.LogDebugAsync($"Setting PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds '{timeoutSeconds}'");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        await client.ConnectAsync(timeout.Token);
        await logger.LogDebugAsync($"Connected to named pipe '{pipeName}'");

        // Send the PID
        await client.RequestReplyAsync<TestHostProcessPIDRequest, VoidResponse>(
            new TestHostProcessPIDRequest(processHandler.GetCurrentProcess().Id),
            testApplicationCancellationTokenSource.CancellationToken);
        return client;
    }

    protected virtual void AddApplicationMetadata(IServiceProvider serviceProvider, Dictionary<string, object> builderMetadata)
    {
        ITelemetryCollector telemetryService = serviceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = serviceProvider.GetTelemetryInformation();
        if (telemetryInformation.IsEnabled)
        {
            builderMetadata[TelemetryProperties.HostProperties.IsNativeAotPropertyName] = (!serviceProvider.GetRuntimeFeature().IsDynamicCodeSupported).AsTelemetryBool();
            builderMetadata[TelemetryProperties.HostProperties.IsHotReloadPropertyName] = serviceProvider.GetRuntimeFeature().IsHotReloadEnabled.AsTelemetryBool();

            var version = (AssemblyInformationalVersionAttribute?)Assembly.GetExecutingAssembly().GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
            builderMetadata[TelemetryProperties.HostProperties.TestingPlatformVersionPropertyName] = version?.InformationalVersion ?? "unknown";

            string moduleName = Path.GetFileName(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            builderMetadata[TelemetryProperties.HostProperties.TestHostPropertyName] = Sha256Hasher.HashWithNormalizedCasing(moduleName);
            builderMetadata[TelemetryProperties.HostProperties.FrameworkDescriptionPropertyName] = RuntimeInformation.FrameworkDescription;
            builderMetadata[TelemetryProperties.HostProperties.ProcessArchitecturePropertyName] = RuntimeInformation.ProcessArchitecture;
            builderMetadata[TelemetryProperties.HostProperties.OSArchitecturePropertyName] = RuntimeInformation.OSArchitecture;
            builderMetadata[TelemetryProperties.HostProperties.OSDescriptionPropertyName] = RuntimeInformation.OSDescription;
#if NETCOREAPP
            builderMetadata[TelemetryProperties.HostProperties.RuntimeIdentifierPropertyName] = RuntimeInformation.RuntimeIdentifier;
#endif

            builderMetadata[TelemetryProperties.HostProperties.IsDebugBuild] =
#if DEBUG
                TelemetryProperties.True;
#else
                TelemetryProperties.False;
#endif

            builderMetadata[TelemetryProperties.HostProperties.IsDebuggerAttached] = Debugger.IsAttached.AsTelemetryBool();
        }
    }

    private static async Task LogTestHostCreatedAsync(
        IServiceProvider serviceProvider,
        string mode,
        Dictionary<string, object> metrics,
        DateTimeOffset stop)
    {
        ITelemetryCollector telemetryService = serviceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = serviceProvider.GetTelemetryInformation();
        if (telemetryInformation.IsEnabled)
        {
            var metricsObj = new Dictionary<string, object>(metrics)
            {
                [TelemetryProperties.HostProperties.ApplicationModePropertyName] = mode,

                // The incoming dictionary already has the start parts of those events. We report stop here because that is where
                // we send the data, and that is potentially very close to the build being finished, so we rather report the data a bit
                // earlier than splitting them over multiple events.
                [TelemetryProperties.HostProperties.BuildBuilderStop] = stop,
                [TelemetryProperties.HostProperties.CreateBuilderStop] = stop,
            };
            await telemetryService.LogEventAsync(TelemetryEvents.TestHostBuiltEventName, metricsObj);
        }
    }

    private async Task<ITestFramework> BuildTestFrameworkAsync(
        ServiceProvider serviceProvider,
        ITestExecutionRequestFactory testExecutionRequestFactory, ITestFrameworkInvoker testExecutionRequestInvoker,
        ITestExecutionFilterFactory testExecutionFilterFactory, IPlatformOutputDevice platformOutputDisplayService,
        IEnumerable<IDataConsumer> serverPerCallConsumers,
        TestFrameworkManager testFrameworkManager,
        TestHostManager testSessionManager,
        MessageBusProxy guardMessageBusService,
        bool isForDiscoveryRequest)
    {
        // Add the message bus proxy
        serviceProvider.AddService(guardMessageBusService);

        // Create the test framework capabilities
        ITestFrameworkCapabilities testFrameworkCapabilities = testFrameworkManager.TestFrameworkCapabilitiesFactory(serviceProvider);
        if (testFrameworkCapabilities is IAsyncInitializableExtension testFrameworkCapabilitiesAsyncInitializable)
        {
            await testFrameworkCapabilitiesAsyncInitializable.InitializeAsync();
        }

        // Register the test framework capabilities to be used by services
        serviceProvider.AddService(testFrameworkCapabilities);

        // Build and register "common non special" services - we need special treatment because extensions can start to log during the
        // creations and we could lose interesting diagnostic information.
        List<IDataConsumer> dataConsumersBuilder = [];

        await RegisterAsServiceOrConsumerOrBothAsync(platformOutputDisplayService, serviceProvider, dataConsumersBuilder);
        await RegisterAsServiceOrConsumerOrBothAsync(testExecutionRequestFactory, serviceProvider, dataConsumersBuilder);
        await RegisterAsServiceOrConsumerOrBothAsync(testExecutionRequestInvoker, serviceProvider, dataConsumersBuilder);
        await RegisterAsServiceOrConsumerOrBothAsync(testExecutionFilterFactory, serviceProvider, dataConsumersBuilder);

        // Create the test framework adapter
        ITestFramework testFrameworkAdapter = testFrameworkManager.TestFrameworkAdapterFactory(testFrameworkCapabilities, serviceProvider);
        if (testFrameworkAdapter is IAsyncInitializableExtension testFrameworkAsyncInitializable)
        {
            await testFrameworkAsyncInitializable.InitializeAsync();
        }

        serviceProvider.AllowTestAdapterFrameworkRegistration = true;
        try
        {
            await RegisterAsServiceOrConsumerOrBothAsync(new TestFrameworkProxy(testFrameworkAdapter), serviceProvider, dataConsumersBuilder);
        }
        finally
        {
            serviceProvider.AllowTestAdapterFrameworkRegistration = false;
        }

        // Virtual callback that allows to the VSTest mode to register custom services needed by the bridge.
        AfterTestAdapterCreation(serviceProvider);

        // Prepare the session lifetime handlers for the notifications
        List<ITestSessionLifetimeHandler> testSessionLifetimeHandlers = [];

        // When we run a discovery request we don't want to run any user extensions, the only needed extension is the test adapter that will "discover" tests.
        if (!isForDiscoveryRequest)
        {
            // We keep the bag of the already created composite service factory to reuse the instance.
            List<ICompositeExtensionFactory> newBuiltCompositeServices = [];
            (IExtension Consumer, int RegistrationOrder)[] consumers = await testSessionManager.BuildDataConsumersAsync(serviceProvider, newBuiltCompositeServices);
            (IExtension TestSessionLifetimeHandler, int RegistrationOrder)[] sessionLifeTimeHandlers = await testSessionManager.BuildTestSessionLifetimeHandleAsync(serviceProvider, newBuiltCompositeServices);

            // Register the test session lifetime handlers for the notifications
            testSessionLifetimeHandlers.AddRange(sessionLifeTimeHandlers.OrderBy(x => x.RegistrationOrder).Select(x => (ITestSessionLifetimeHandler)x.TestSessionLifetimeHandler));

            // Keep the registration order
            foreach ((IExtension Extension, int _) testhostExtension in consumers.Union(sessionLifeTimeHandlers).OrderBy(x => x.RegistrationOrder))
            {
                if (testhostExtension.Extension is IDataConsumer)
                {
                    await RegisterAsServiceOrConsumerOrBothAsync(testhostExtension.Extension, serviceProvider, dataConsumersBuilder);
                }
                else
                {
                    await AddServiceIfNotSkippedAsync(testhostExtension.Extension, serviceProvider);
                }
            }
        }

        // In server mode where we don't shutdown the process at the end of the run we need a way to use a "per-call" consumer to be able to link the
        // node updates to the correct request id.
        foreach (IDataConsumer consumerService in serverPerCallConsumers)
        {
            if (consumerService is ITestSessionLifetimeHandler handler)
            {
                testSessionLifetimeHandlers.Add(handler);
            }

            await RegisterAsServiceOrConsumerOrBothAsync(consumerService, serviceProvider, dataConsumersBuilder);
        }

        // Register the test session lifetime handlers container
        TestSessionLifetimeHandlersContainer testSessionLifetimeHandlersContainer = new(testSessionLifetimeHandlers);
        serviceProvider.AddService(testSessionLifetimeHandlersContainer);

        // Register the ITestApplicationResult
        var testApplicationResult = new TestApplicationResult(
            platformOutputDisplayService,
            serviceProvider.GetTestApplicationCancellationTokenSource(),
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetEnvironment());
        await RegisterAsServiceOrConsumerOrBothAsync(testApplicationResult, serviceProvider, dataConsumersBuilder);

        IDataConsumer[] dataConsumerServices = dataConsumersBuilder.ToArray();

        // Build the message hub
        // If we're running discovery command line we don't want to process any messages coming from the adapter so we filter out all extensions
        // adding a custom message bus that will simply forward to the output display for better performance
        if (serviceProvider.GetCommandLineOptions().IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey))
        {
            var concreteMessageBusService = new ListTestsMessageBus(
                serviceProvider.GetTestFramework(),
                serviceProvider.GetTestApplicationCancellationTokenSource(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetOutputDevice(),
                serviceProvider.GetAsyncMonitorFactory(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetTestApplicationProcessExitCode());
            await concreteMessageBusService.InitAsync();
            guardMessageBusService.SetBuiltMessageBus(concreteMessageBusService);
        }
        else
        {
            var concreteMessageBusService = new AsynchronousMessageBus(
                dataConsumerServices,
                serviceProvider.GetTestApplicationCancellationTokenSource(),
                serviceProvider.GetTask(),
                serviceProvider.GetLoggerFactory(),
                serviceProvider.GetEnvironment());
            await concreteMessageBusService.InitAsync();
            guardMessageBusService.SetBuiltMessageBus(concreteMessageBusService);
        }

        return testFrameworkAdapter;
    }

    protected virtual ConsoleTestHost CreateConsoleTestHost(
        ServiceProvider serviceProvider,
        Func<ServiceProvider,
                          ITestExecutionRequestFactory, ITestFrameworkInvoker,
                          ITestExecutionFilterFactory, IPlatformOutputDevice,
                          IEnumerable<IDataConsumer>,
                          TestFrameworkManager,
                          TestHostManager,
                          MessageBusProxy,
                          bool,
                          Task<ITestFramework>> buildTestFrameworkAsync,
        TestFrameworkManager testFrameworkManager,
        TestHostManager testHostManager)
        => new(serviceProvider, buildTestFrameworkAsync, testFrameworkManager, testHostManager);

    protected virtual bool SkipAddingService(object service) => false;

    protected virtual void AfterTestAdapterCreation(ServiceProvider serviceProvider)
    {
    }

    private async Task AddServiceIfNotSkippedAsync(object service, ServiceProvider serviceProvider)
    {
        if (!SkipAddingService(service))
        {
            if (service is IExtension extension)
            {
                if (await extension.IsEnabledAsync())
                {
                    serviceProvider.TryAddService(service);
                }
            }
            else
            {
                serviceProvider.TryAddService(service);
            }
        }
    }

    private async Task RegisterAsServiceOrConsumerOrBothAsync(object service, ServiceProvider serviceProvider,
        List<IDataConsumer> dataConsumersBuilder)
    {
        if (service is IDataConsumer dataConsumer)
        {
            if (!await dataConsumer.IsEnabledAsync())
            {
                return;
            }

            dataConsumersBuilder.Add(dataConsumer);
        }

        if (service is IOutputDevice)
        {
            return;
        }

        await AddServiceIfNotSkippedAsync(service, serviceProvider);
    }
}
