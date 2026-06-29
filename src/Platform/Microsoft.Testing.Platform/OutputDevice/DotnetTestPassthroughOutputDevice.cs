// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// The host output device used under the dotnet test pipe protocol. Like
/// <see cref="NopPlatformOutputDevice"/> it discards regular host output (the SDK's TerminalTestReporter
/// owns user-facing rendering), but it additionally forwards lines marked with
/// <see cref="AzureDevOpsCommandOutputDeviceData"/> to the SDK as <see cref="AzureDevOpsLogMessage"/> so
/// the AzureDevOpsReport extension's logging commands (##[group], ##vso[...]) still reach the pipeline
/// log in multi-assembly runs.
/// </summary>
/// <remarks>
/// Forwarding is gated on the SDK negotiating protocol version 1.2.0 or later
/// (<see cref="DotnetTestConnection.IsLogForwardingSupported"/>); against an older SDK the marked lines
/// are swallowed exactly like the no-op device, so no unknown message id is ever sent. The
/// <see cref="DotnetTestConnection"/> and the dotnet test execution id are resolved lazily because both
/// become available only after the output device is built (the connection is created and the execution
/// id env var is set during <c>AfterCommonServiceSetupAsync</c>).
/// </remarks>
internal sealed class DotnetTestPassthroughOutputDevice : IPlatformOutputDevice
{
    private readonly IServiceProvider _serviceProvider;

    public DotnetTestPassthroughOutputDevice(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public string Uid => nameof(DotnetTestPassthroughOutputDevice);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(DotnetTestPassthroughOutputDevice);

    public string Description => "Output device that discards host output but forwards Azure DevOps logging commands to the SDK under the dotnet test pipe protocol.";

    // Returning false keeps this device from being registered as a data consumer, matching NopPlatformOutputDevice.
    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        // Preserve the deliberate pipe-protocol suppression: only Azure DevOps command lines are
        // forwarded; everything else is swallowed exactly like NopPlatformOutputDevice.
        if (data is not AzureDevOpsCommandOutputDeviceData commandData)
        {
            return;
        }

        // The dotnet test pipe protocol (DotnetTestConnection) is never active on browser, so the
        // browser-unsupported members below are unreachable there; suppress CA1416 accordingly.
#pragma warning disable CA1416 // Validate platform compatibility
        if (_serviceProvider.GetService<IPushOnlyProtocol>() is not DotnetTestConnection connection
            || !connection.IsLogForwardingSupported)
        {
            return;
        }

        string? executionId = _serviceProvider.GetEnvironment().GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
        await connection.SendMessageAsync(new AzureDevOpsLogMessage(executionId, DotnetTestConnection.InstanceId, commandData.Text)).ConfigureAwait(false);
#pragma warning restore CA1416 // Validate platform compatibility
    }

    public Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken) => Task.CompletedTask;
}
