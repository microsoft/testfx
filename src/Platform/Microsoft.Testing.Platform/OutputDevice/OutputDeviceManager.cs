// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;
#if !NET7_0_OR_GREATER
using Microsoft.Testing.Platform.Resources;
#endif
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

internal sealed class PlatformOutputDeviceManager
{
    private Func<IServiceProvider, IPlatformOutputDevice>? _platformOutputDeviceFactory;

    public void SetPlatformOutputDevice(Func<IServiceProvider, IPlatformOutputDevice> platformOutputDeviceFactory)
        => _platformOutputDeviceFactory = platformOutputDeviceFactory ?? throw new ArgumentNullException(nameof(platformOutputDeviceFactory));

    internal async Task<ProxyOutputDevice> BuildAsync(ServiceProvider serviceProvider, bool useServerModeOutputDevice, bool isPipeProtocol)
    {
        // Under the dotnet test pipe protocol, the SDK's TerminalTestReporter owns all
        // user-facing output, so the host must not produce console output of its own. See #7161
        // and dotnet/sdk#51615 for the broader context.
        //
        // Outside Azure DevOps there is nothing to forward, so we keep the pure no-op device. Under
        // Azure DevOps the AzureDevOpsReport extension produces logging commands (##[group],
        // ##vso[...]) that must still reach the pipeline log; DotnetTestPassthroughOutputDevice
        // forwards those marked lines to the SDK over the protocol while discarding everything else.
        // The forwarder gates on the agent only (TF_BUILD), NOT the TESTINGPLATFORM_AZDO_OUTPUT opt-out:
        // that opt-out is scoped to the platform's automatic ##vso[task.logissue] emission, and honoring
        // it here would make multi-assembly forwarding inconsistent with single-assembly runs (where the
        // extension's output is gated on TF_BUILD alone).
        if (isPipeProtocol)
        {
            IPlatformOutputDevice pipeProtocolOutputDevice =
                AzureDevOpsLogIssueFormatter.IsAzureDevOpsAgent(serviceProvider.GetEnvironment())
                    ? new DotnetTestPassthroughOutputDevice(serviceProvider)
                    : new NopPlatformOutputDevice();

            return new ProxyOutputDevice(pipeProtocolOutputDevice, serverModeOutputDevice: null);
        }

        // SetPlatformOutputDevice isn't public yet. Before exposing it, we should decide
        // whether to pass the "useServerModeOutputDevice" info to it.
        IPlatformOutputDevice nonServerOutputDevice = _platformOutputDeviceFactory is null
            ? GetDefaultTerminalOutputDevice(serviceProvider)
            : _platformOutputDeviceFactory(serviceProvider);

        // If the externally provided output device is not enabled, we opt-in the default terminal output device.
        if (_platformOutputDeviceFactory is not null && !await nonServerOutputDevice.IsEnabledAsync().ConfigureAwait(false))
        {
            nonServerOutputDevice = GetDefaultTerminalOutputDevice(serviceProvider);
        }

        return new ProxyOutputDevice(
            nonServerOutputDevice,
            useServerModeOutputDevice
                ? new ServerModePerCallOutputDevice(
                    serviceProvider.GetService<FileLoggerProvider>(),
                    serviceProvider.GetRequiredService<IStopPoliciesService>())
                : null);
    }

    public static IPlatformOutputDevice GetDefaultTerminalOutputDevice(ServiceProvider serviceProvider)
    {
        if (OperatingSystem.IsBrowser())
        {
#if NET7_0_OR_GREATER
            return new BrowserOutputDevice(
                serviceProvider.GetConsole(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetAsyncMonitorFactory().Create(),
                serviceProvider.GetRuntimeFeature(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetPlatformInformation(),
                serviceProvider.GetRequiredService<IStopPoliciesService>());
#else
            throw new PlatformNotSupportedException(PlatformResources.BrowserPlatformNotSupportedOnOlderFrameworks);
#endif
        }

        if (OperatingSystem.IsWasi())
        {
            return new WasiOutputDevice(
                serviceProvider.GetConsole(),
                serviceProvider.GetTestApplicationModuleInfo(),
                serviceProvider.GetAsyncMonitorFactory().Create(),
                serviceProvider.GetRuntimeFeature(),
                serviceProvider.GetEnvironment(),
                serviceProvider.GetPlatformInformation(),
                serviceProvider.GetRequiredService<IStopPoliciesService>());
        }

        // Default to terminal output device for other platforms.
        return new TerminalOutputDevice(
            serviceProvider.GetConsole(),
            serviceProvider.GetTestApplicationModuleInfo(),
            serviceProvider.GetTestHostControllerInfo(),
            serviceProvider.GetAsyncMonitorFactory().Create(),
            serviceProvider.GetRuntimeFeature(),
            serviceProvider.GetEnvironment(),
            serviceProvider.GetPlatformInformation(),
            serviceProvider.GetCommandLineOptions(),
            serviceProvider.GetFileLoggerInformation(),
            serviceProvider.GetLoggerFactory(),
            serviceProvider.GetClock(),
            serviceProvider.GetRequiredService<IStopPoliciesService>(),
            serviceProvider.GetTestApplicationCancellationTokenSource());
    }
}
