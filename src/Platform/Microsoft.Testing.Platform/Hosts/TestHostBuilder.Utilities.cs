// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class TestHostBuilder
{
    private static async Task<NamedPipeClient?> ConnectToTestHostProcessMonitorIfAvailableAsync(
        CTRLPlusCCancellationTokenSource testApplicationCancellationTokenSource,
        ILogger logger,
        TestHostControllerInfo testHostControllerInfo,
        AggregatedConfiguration configuration,
        SystemEnvironment environment)
    {
        if (!testHostControllerInfo.HasTestHostController)
        {
            return null;
        }

        if (OperatingSystem.IsBrowser())
        {
            logger.LogWarning($"Test Host Controller connection is not supported on WebAssembly targets.");
            return null;
        }

        string pipeEnvironmentVariable = $"{EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME}_{testHostControllerInfo.GetTestHostControllerPID()}";
        string pipeName = environment.GetEnvironmentVariable(pipeEnvironmentVariable) ?? throw new InvalidOperationException($"Unexpected null pipe name from environment variable '{pipeEnvironmentVariable}'");

        environment.SetEnvironmentVariable(pipeEnvironmentVariable, string.Empty);

        NamedPipeClient client = new(pipeName, environment);
        client.RegisterAllSerializers();

        await logger.LogDebugAsync($"Connecting to named pipe '{pipeName}'").ConfigureAwait(false);
        string? seconds = configuration[PlatformConfigurationConstants.PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds];

        double timeoutSeconds = seconds is null ? TimeoutHelper.DefaultHangTimeoutSeconds : double.Parse(seconds, CultureInfo.InvariantCulture);
        await logger.LogDebugAsync($"Setting PlatformTestHostControllersManagerNamedPipeClientConnectTimeoutSeconds '{timeoutSeconds}'").ConfigureAwait(false);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
        await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
        await logger.LogDebugAsync($"Connected to named pipe '{pipeName}'").ConfigureAwait(false);

        await client.RequestReplyAsync<TestHostProcessPIDRequest, VoidResponse>(
            new TestHostProcessPIDRequest(environment.ProcessId),
            testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
        return client;
    }

    private void AddApplicationTelemetryMetadata(IServiceProvider serviceProvider, Dictionary<string, object> builderMetadata)
    {
        ITelemetryInformation telemetryInformation = serviceProvider.GetTelemetryInformation();
        if (!telemetryInformation.IsEnabled)
        {
            return;
        }

        builderMetadata[TelemetryProperties.HostProperties.IsNativeAotPropertyName] = (!serviceProvider.GetRuntimeFeature().IsDynamicCodeSupported).AsTelemetryBool();
        builderMetadata[TelemetryProperties.HostProperties.IsHotReloadPropertyName] = serviceProvider.GetRuntimeFeature().IsHotReloadEnabled.AsTelemetryBool();

        AssemblyInformationalVersionAttribute? version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        builderMetadata[TelemetryProperties.HostProperties.TestingPlatformVersionPropertyName] = version?.InformationalVersion ?? "unknown";

        string moduleName = Path.GetFileName(_testApplicationModuleInfo.TryGetCurrentTestApplicationFullPath())
            ?? _testApplicationModuleInfo.TryGetAssemblyName()
            ?? "unknown";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("WASI")))
        {
            builderMetadata[TelemetryProperties.HostProperties.TestHostPropertyName] = Sha256Hasher.HashWithNormalizedCasing(moduleName);
        }

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

    private static async Task LogTestHostCreatedAsync(
        IServiceProvider serviceProvider,
        string mode,
        Dictionary<string, object> metrics,
        DateTimeOffset stop,
        CancellationToken cancellationToken)
    {
        ITelemetryCollector telemetryService = serviceProvider.GetTelemetryCollector();
        ITelemetryInformation telemetryInformation = serviceProvider.GetTelemetryInformation();
        if (!telemetryInformation.IsEnabled)
        {
            return;
        }

        Dictionary<string, object> metricsObj = new(metrics)
        {
            [TelemetryProperties.HostProperties.ApplicationModePropertyName] = mode,
            [TelemetryProperties.HostProperties.BuildBuilderStop] = stop,
            [TelemetryProperties.HostProperties.CreateBuilderStop] = stop,
        };
        await telemetryService.LogEventAsync(TelemetryEvents.TestHostBuiltEventName, metricsObj, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddServiceIfNotSkippedAsync(object service, ServiceProvider serviceProvider)
    {
        if (service is IExtension extension)
        {
            if (await extension.IsEnabledAsync().ConfigureAwait(false))
            {
                serviceProvider.TryAddService(service);
            }

            return;
        }

        serviceProvider.TryAddService(service);
    }

    private static async Task RegisterAsServiceOrConsumerOrBothAsync(
        object service,
        ServiceProvider serviceProvider,
        List<IDataConsumer> dataConsumersBuilder)
    {
        if (service is IDataConsumer dataConsumer)
        {
            if (!await dataConsumer.IsEnabledAsync().ConfigureAwait(false))
            {
                return;
            }

            dataConsumersBuilder.Add(dataConsumer);
        }

        if (service is IOutputDevice)
        {
            return;
        }

        await AddServiceIfNotSkippedAsync(service, serviceProvider).ConfigureAwait(false);
    }

    private async Task DisplayBannerIfEnabledAsync(
        ICommandLineOptions commandLineOptions,
        ProxyOutputDevice outputDevice,
        ITestFrameworkCapabilities testFrameworkCapabilities,
        CancellationToken cancellationToken)
    {
        // Read --no-banner from the unified command-line view so testconfig.json entries such as
        // "no-banner": true are honored. Falling back to the raw parse result here (issue #6349)
        // would only consider CLI input and silently ignore the JSON-backed value.
        bool isNoBannerSet = commandLineOptions.IsOptionSet(PlatformCommandLineProvider.NoBannerOptionKey);
        string? noBannerEnvironmentVar = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NOBANNER);
        string? dotnetNoLogoEnvironmentVar = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_NOLOGO);

        // Skip the banner under detected LLM/AI agent environments to reduce token noise.
        // To force the banner back on in an LLM environment, clear the LLM env var (or use a non-LLM shell).
        bool isLLMEnvironment = new LLMEnvironmentDetector(_environment).IsLLMEnvironment();

        if (!isNoBannerSet && !(noBannerEnvironmentVar is "1" or "true") && !(dotnetNoLogoEnvironmentVar is "1" or "true") && !isLLMEnvironment)
        {
            IBannerMessageOwnerCapability? bannerMessageOwnerCapability = testFrameworkCapabilities.GetCapability<IBannerMessageOwnerCapability>();
            string? bannerMessage = bannerMessageOwnerCapability is not null
                ? await bannerMessageOwnerCapability.GetBannerMessageAsync().ConfigureAwait(false)
                : null;

            await outputDevice.DisplayBannerAsync(bannerMessage, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void CompleteBuilderActivity(IPlatformActivity? builderActivity, string hostType)
    {
        builderActivity?.SetTag(BuilderHostTypeOTelKey, hostType);
        builderActivity?.Dispose();
    }
}
