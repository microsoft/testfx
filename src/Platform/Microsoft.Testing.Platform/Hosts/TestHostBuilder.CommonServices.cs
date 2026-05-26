// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private async Task<BuildContext> SetupCommonServicesAsync(
        ApplicationLoggingState loggingState,
        TestApplicationOptions testApplicationOptions,
        IUnhandledExceptionsHandler unhandledExceptionsHandler,
        DateTimeOffset createBuilderStart)
    {
        // ============= SETUP COMMON SERVICE USED IN ALL MODES ===============//
        SystemClock systemClock = new();
        DateTimeOffset buildBuilderStart = systemClock.UtcNow;
        Dictionary<string, object> builderMetrics = new()
        {
            [TelemetryProperties.HostProperties.CreateBuilderStart] = createBuilderStart,
            [TelemetryProperties.HostProperties.BuildBuilderStart] = buildBuilderStart,
        };
        SystemEnvironment systemEnvironment = new();
        ServiceProvider serviceProvider = new();
        TestHostControllerInfo testHostControllerInfo = new(loggingState.CommandLineParseResult);
        BuildContext context = new(
            loggingState,
            testApplicationOptions,
            unhandledExceptionsHandler,
            createBuilderStart,
            systemClock,
            buildBuilderStart,
            builderMetrics,
            systemEnvironment,
            serviceProvider,
            testHostControllerInfo);

        if (loggingState.FileLoggerProvider is not null)
        {
            serviceProvider.TryAddService(loggingState.FileLoggerProvider);
        }

        serviceProvider.TryAddService(loggingState);
        serviceProvider.TryAddService(systemEnvironment);
        SystemConsole systemConsole = new();
        serviceProvider.TryAddService(systemConsole);
        SystemTask systemTask = new();
        serviceProvider.TryAddService(systemTask);
        serviceProvider.TryAddService(_runtimeFeature);
        serviceProvider.TryAddService(_processHandler);
        serviceProvider.TryAddService(_fileSystem);
        serviceProvider.TryAddService(_testApplicationModuleInfo);
        serviceProvider.TryAddService(testHostControllerInfo);
        serviceProvider.TryAddService(systemClock);

        SystemMonitor systemMonitor = new();
        serviceProvider.TryAddService(systemMonitor);
        SystemMonitorAsyncFactory systemMonitorAsyncFactory = new();
        serviceProvider.TryAddService(systemMonitorAsyncFactory);
        context.SystemMonitorAsyncFactory = systemMonitorAsyncFactory;

        serviceProvider.AddService(new PlatformInformation());

        ILogger? logger = null;
        if (loggingState.FileLoggerProvider is not null)
        {
            logger = loggingState.FileLoggerProvider.CreateLogger(GetType().ToString());
            FileLoggerInformation fileLoggerInformation = new(loggingState.FileLoggerProvider.SyncFlush, new(loggingState.FileLoggerProvider.FileLogger.FileName), loggingState.LogLevel);
            serviceProvider.TryAddService(fileLoggerInformation);
        }

        if (logger is not null)
        {
            await logger.LogInformationAsync($"Setting RegisterEnvironmentVariablesConfigurationSource: '{testApplicationOptions.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource}'").ConfigureAwait(false);
        }

        if (testApplicationOptions.Configuration.ConfigurationSources.RegisterEnvironmentVariablesConfigurationSource)
        {
            Configuration.AddConfigurationSource(() => new EnvironmentVariablesConfigurationSource(systemEnvironment));
        }

        Configuration.AddConfigurationSource(() => new JsonConfigurationSource(_testApplicationModuleInfo, _fileSystem, loggingState.FileLoggerProvider));

        context.Configuration = (AggregatedConfiguration)await ((ConfigurationManager)Configuration).BuildAsync(loggingState.FileLoggerProvider, loggingState.CommandLineParseResult).ConfigureAwait(false);
        serviceProvider.TryAddService(context.Configuration);

#pragma warning disable CA1416 // Browser does not support the test host controller process model. The provider itself is marked [UnsupportedOSPlatform("browser")].
        if (!OperatingSystem.IsBrowser())
        {
            // Insert the built-in provider at the front so later user-supplied providers -
            // including the VSTest bridge runsettings provider - can still override these
            // unlocked values when both sources are present.
            if (TestHostControllers is TestHostControllersManager testHostControllersManager)
            {
                testHostControllersManager.AddEnvironmentVariableProviderFirst(sp => new TestConfigurationEnvironmentVariableProvider(sp.GetConfiguration()));
            }
            else
            {
                throw ApplicationStateGuard.Unreachable();
            }
        }
#pragma warning restore CA1416

        if (((TelemetryManager)Telemetry).BuildOTelProvider(serviceProvider) is { } otelService)
        {
            serviceProvider.AddService(otelService);
            context.BuilderActivity = serviceProvider.GetServiceInternal<IPlatformOpenTelemetryService>()?.StartActivity("TestHostBuilder", startTime: buildBuilderStart);
        }

        _ = bool.TryParse(context.Configuration[PlatformConfigurationConstants.PlatformExitProcessOnUnhandledException], out bool isFileConfiguredToFailFast);

        string? environmentSetting = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION);
        bool? isEnvConfiguredToFailFast = null;
        if (environmentSetting is "1" or "0")
        {
            isEnvConfiguredToFailFast = environmentSetting == "1";
        }

        bool exitProcessOnUnhandledException = isEnvConfiguredToFailFast is not null ? environmentSetting == "1" : isFileConfiguredToFailFast;

        if (logger is not null)
        {
            await logger.LogInformationAsync($"Setting PlatformExitProcessOnUnhandledException: '{exitProcessOnUnhandledException}', config file: {isFileConfiguredToFailFast} environment variable: {isEnvConfiguredToFailFast}").ConfigureAwait(false);
        }

        if (exitProcessOnUnhandledException)
        {
            unhandledExceptionsHandler.Subscribe();
        }

        serviceProvider.AddService(new UnhandledExceptionsPolicy(fastFailOnFailure: exitProcessOnUnhandledException));

        context.TestApplicationCancellationTokenSource = new CTRLPlusCCancellationTokenSource(
            systemConsole,
            loggingState.FileLoggerProvider?.CreateLogger(nameof(CTRLPlusCCancellationTokenSource)));
        serviceProvider.AddService(context.TestApplicationCancellationTokenSource, throwIfSameInstanceExit: true);

        CommandLineOptionsProxy commandLineOptionsProxy = new();
        serviceProvider.TryAddService(commandLineOptionsProxy);

        LoggerFactoryProxy loggerFactoryProxy = new();
        serviceProvider.TryAddService(loggerFactoryProxy);

        context.CommandLineHandler = await ((CommandLineManager)CommandLine).BuildAsync(loggingState.CommandLineParseResult, serviceProvider).ConfigureAwait(false);
        commandLineOptionsProxy.SetCommandLineOptions(context.CommandLineHandler);

        context.PoliciesService = new StopPoliciesService(context.TestApplicationCancellationTokenSource);
        serviceProvider.AddService(context.PoliciesService);

        context.HasServerFlag = context.CommandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? protocolName);
        context.IsJsonRpcProtocol = protocolName is null
            || protocolName.Length == 0
            || protocolName[0].Equals(PlatformCommandLineProvider.JsonRpcProtocolName, StringComparison.OrdinalIgnoreCase);

        context.ProxyOutputDevice = await _outputDisplay.BuildAsync(serviceProvider, context.HasServerFlag && context.IsJsonRpcProtocol).ConfigureAwait(false);

        if (loggingState.FileLoggerProvider is not null)
        {
            Logging.AddProvider((_, _) => loggingState.FileLoggerProvider);
        }

        ICommandLineOptions commandLineOptions = serviceProvider.GetCommandLineOptions();
        context.LoggerFactory = await ((LoggingManager)Logging).BuildAsync(serviceProvider, loggingState.LogLevel, systemMonitor).ConfigureAwait(false);
        loggerFactoryProxy.SetLoggerFactory(context.LoggerFactory);

        if (await context.ProxyOutputDevice.OriginalOutputDevice.IsEnabledAsync().ConfigureAwait(false))
        {
            await context.ProxyOutputDevice.OriginalOutputDevice.TryInitializeAsync().ConfigureAwait(false);
        }

        serviceProvider.TryAddService(context.ProxyOutputDevice);
        serviceProvider.TryAddService(context.ProxyOutputDevice.OriginalOutputDevice);

        context.TestFrameworkCapabilities = TestFramework!.TestFrameworkCapabilitiesFactory(serviceProvider);
        if (context.TestFrameworkCapabilities is IAsyncInitializableExtension testFrameworkCapabilitiesAsyncInitializable)
        {
            await testFrameworkCapabilitiesAsyncInitializable.InitializeAsync().ConfigureAwait(false);
        }

        serviceProvider.AddService(context.TestFrameworkCapabilities);

        ValidationResult commandLineValidationResult = await CommandLineOptionsValidator.ValidateAsync(
            loggingState.CommandLineParseResult,
            context.CommandLineHandler.SystemCommandLineOptionsProviders,
            context.CommandLineHandler.ExtensionsCommandLineOptionsProviders,
            context.CommandLineHandler).ConfigureAwait(false);

        if (!loggingState.CommandLineParseResult.HasTool && !commandLineValidationResult.IsValid)
        {
            await DisplayBannerIfEnabledAsync(loggingState, context.ProxyOutputDevice, context.TestFrameworkCapabilities, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            await context.ProxyOutputDevice.DisplayAsync(context.CommandLineHandler, new ErrorMessageOutputDeviceData(commandLineValidationResult.ErrorMessage), context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
            await context.CommandLineHandler.PrintHelpAsync(context.ProxyOutputDevice, null, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

            CompleteBuilderActivity(context.BuilderActivity, nameof(InformativeCommandLineHost));
            context.EarlyHost = new InformativeCommandLineHost((int)ExitCode.InvalidCommandLine, serviceProvider);
            return context;
        }

        serviceProvider.TryAddService(context.CommandLineHandler);

        if (commandLineOptions.IsOptionSet(PlatformCommandLineProvider.TimeoutOptionKey)
            && commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.TimeoutOptionKey, out string[]? args))
        {
            if (!TimeSpanParser.TryParseRequireSuffix(args[0], out TimeSpan timeout))
            {
                throw ApplicationStateGuard.Unreachable();
            }

            context.TestApplicationCancellationTokenSource.CancelAfter(timeout);
        }

        context.IsHelpCommand = context.CommandLineHandler.IsHelpInvoked();
        context.IsInfoCommand = context.CommandLineHandler.IsInfoInvoked();

        if (!context.IsHelpCommand && !context.IsInfoCommand)
        {
            await context.Configuration.CheckTestResultsDirectoryOverrideAndCreateItAsync(loggingState.FileLoggerProvider).ConfigureAwait(false);
        }

        await DisplayBannerIfEnabledAsync(loggingState, context.ProxyOutputDevice, context.TestFrameworkCapabilities, context.TestApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);

        ITelemetryCollector telemetryService = await ((TelemetryManager)Telemetry).BuildTelemetryAsync(serviceProvider, context.LoggerFactory, testApplicationOptions).ConfigureAwait(false);
        serviceProvider.TryAddService(telemetryService);

        AddApplicationTelemetryMetadata(serviceProvider, builderMetrics);

        if (commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ExitOnProcessExitOptionKey))
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException(PlatformResources.PlatformCommandLineExitOnProcessExitNotSupportedInBrowser);
            }

            serviceProvider.AddService(new NonCooperativeParentProcessListener(commandLineOptions, _environment));
        }

        serviceProvider.AddService(new TestApplicationResult(
            context.ProxyOutputDevice,
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetEnvironment(),
            context.PoliciesService,
            serviceProvider.GetPlatformOTelService()));

        ChatClientManager.BuildChatClients(serviceProvider);

        return context;
    }
}
