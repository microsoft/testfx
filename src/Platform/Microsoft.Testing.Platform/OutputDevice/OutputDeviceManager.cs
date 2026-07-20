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
        // DotnetTestPassthroughOutputDevice discards regular informational output exactly like a no-op device, but
        // forwards three classes of host message to the SDK over the protocol so they are not swallowed in
        // multi-assembly runs:
        //   - Durable session messages (SessionMessageOutputDeviceData) as informational DisplayMessage
        //     (protocol 1.3.0+).
        //   - Warning/error host messages (WarningMessageOutputDeviceData / ErrorMessageOutputDeviceData) as
        //     DisplayMessage (protocol 1.3.0+). This covers hang/crash dump diagnostics, retry summaries, and
        //     generic extension/framework warnings and errors — regardless of environment.
        //   - Azure DevOps logging commands (##[group], ##vso[...]) produced by the AzureDevOpsReport extension as
        //     AzureDevOpsLogMessage (protocol 1.2.0+). The extension only produces these on an Azure DevOps agent
        //     (TF_BUILD), so off-agent that branch is naturally inert; the device itself is installed everywhere so
        //     the warning/error forwarding above is not gated on the agent. The Azure DevOps forwarder gates on the
        //     agent only (TF_BUILD), NOT the TESTINGPLATFORM_AZDO_OUTPUT opt-out: that opt-out is scoped to the
        //     platform's automatic ##vso[task.logissue] emission, and honoring it here would make multi-assembly
        //     forwarding inconsistent with single-assembly runs.
        if (isPipeProtocol)
        {
            return new ProxyOutputDevice(new DotnetTestPassthroughOutputDevice(serviceProvider), serverModeOutputDevice: null);
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
