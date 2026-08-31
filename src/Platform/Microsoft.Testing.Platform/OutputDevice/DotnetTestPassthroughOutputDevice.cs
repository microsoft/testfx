// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// The host output device used under the dotnet test pipe protocol. Like
/// <see cref="NopPlatformOutputDevice"/> it discards regular (informational) host output (the SDK's
/// TerminalTestReporter owns user-facing rendering), but it additionally forwards three classes of host message to
/// the SDK over the pipe so they are not swallowed in multi-assembly runs:
/// <list type="bullet">
/// <item><see cref="SessionMessageOutputDeviceData"/> is forwarded as an informational
/// <see cref="DisplayMessage"/> so durable framework and extension messages reach the SDK-owned terminal reporter
/// (protocol 1.3.0+).</item>
/// <item>Lines marked with <see cref="AzureDevOpsCommandOutputDeviceData"/> are forwarded verbatim as
/// <see cref="AzureDevOpsLogMessage"/> so the AzureDevOpsReport extension's logging commands (##[group],
/// ##vso[...]) still reach the pipeline log (protocol 1.2.0+, only on an Azure DevOps agent where the extension
/// produces them).</item>
/// <item><see cref="WarningMessageOutputDeviceData"/> and <see cref="ErrorMessageOutputDeviceData"/> are
/// forwarded as <see cref="DisplayMessage"/> so host-side diagnostics produced outside test results (hang/crash
/// dump diagnostics, retry summaries, generic extension/framework warnings and errors) reach the SDK, which
/// routes them to its TerminalTestReporter's WriteWarningMessage / WriteErrorMessage (protocol 1.3.0+).</item>
/// </list>
/// </summary>
/// <remarks>
/// Forwarding is gated on the SDK negotiating the relevant protocol version
/// (<see cref="DotnetTestConnection.IsLogForwardingSupported"/> for Azure DevOps commands,
/// <see cref="DotnetTestConnection.IsDisplayMessageForwardingSupported"/> for display messages); against an older
/// SDK the corresponding lines are swallowed exactly like the no-op device, so no unknown message id is ever
/// sent. Plain informational output (<see cref="TextOutputDeviceData"/> /
/// <see cref="FormattedTextOutputDeviceData"/>) is still discarded; only explicitly durable session messages,
/// warnings, and errors cross the wire. The
/// <see cref="DotnetTestConnection"/> and the dotnet test execution id are resolved lazily because both become
/// available only after the output device is built (the connection is created and the execution id env var is
/// set during <c>AfterCommonServiceSetupAsync</c>).
/// </remarks>
internal sealed class DotnetTestPassthroughOutputDevice : IPlatformOutputDevice
{
    private readonly IServiceProvider _serviceProvider;

    public DotnetTestPassthroughOutputDevice(IServiceProvider serviceProvider)
        => _serviceProvider = serviceProvider;

    public string Uid => nameof(DotnetTestPassthroughOutputDevice);

    public string Version => PlatformVersion.Version;

    public string DisplayName => nameof(DotnetTestPassthroughOutputDevice);

    public string Description => "Output device that discards regular informational host output but forwards durable session messages, Azure DevOps logging commands, and warning/error messages to the SDK under the dotnet test pipe protocol.";

    // Returning false keeps this device from being registered as a data consumer, matching NopPlatformOutputDevice.
    public Task<bool> IsEnabledAsync() => Task.FromResult(false);

    public async Task DisplayAsync(IOutputDeviceDataProducer producer, IOutputDeviceData data, CancellationToken cancellationToken)
    {
        // Preserve the deliberate pipe-protocol suppression: durable session messages, Azure DevOps command lines,
        // and warning/error messages are forwarded; regular informational text is swallowed exactly like
        // NopPlatformOutputDevice. The switch returns early for non-forwarded data WITHOUT touching the service
        // provider, so regular informational output stays as cheap as the no-op device.
        switch (data)
        {
            case SessionMessageOutputDeviceData sessionMessageData:
                await ForwardDisplayMessageAsync(DisplayMessageLevels.Information, sessionMessageData.Message, cancellationToken).ConfigureAwait(false);
                break;

            case AzureDevOpsCommandOutputDeviceData commandData:
                await ForwardAzureDevOpsAsync(commandData, cancellationToken).ConfigureAwait(false);
                break;

            case ErrorMessageOutputDeviceData errorData:
                await ForwardDisplayMessageAsync(DisplayMessageLevels.Error, errorData.Message, cancellationToken).ConfigureAwait(false);
                break;

            case WarningMessageOutputDeviceData warningData:
                await ForwardDisplayMessageAsync(DisplayMessageLevels.Warning, warningData.Message, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task ForwardAzureDevOpsAsync(AzureDevOpsCommandOutputDeviceData commandData, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IPushOnlyProtocol>() is not DotnetTestConnection connection
            || !connection.IsLogForwardingSupported)
        {
            return;
        }

        await connection.SendMessageAsync(new AzureDevOpsLogMessage(GetExecutionId(), DotnetTestConnection.InstanceId, commandData.Text)).ConfigureAwait(false);
    }

    private async Task ForwardDisplayMessageAsync(byte level, string text, CancellationToken cancellationToken)
    {
        if (_serviceProvider.GetService<IPushOnlyProtocol>() is not DotnetTestConnection connection
            || !connection.IsDisplayMessageForwardingSupported)
        {
            return;
        }

        await connection.SendMessageAsync(new DisplayMessage(GetExecutionId(), DotnetTestConnection.InstanceId, level, text)).ConfigureAwait(false);
    }

    private string? GetExecutionId()
        => _serviceProvider.GetEnvironment().GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);

    public Task DisplayBannerAsync(string? bannerMessage, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayBeforeSessionStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisplayAfterSessionEndRunAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task HandleProcessRoleAsync(TestProcessRole processRole, CancellationToken cancellationToken) => Task.CompletedTask;
}
