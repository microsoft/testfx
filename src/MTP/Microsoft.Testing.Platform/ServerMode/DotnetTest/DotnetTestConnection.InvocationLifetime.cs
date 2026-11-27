// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform;

internal sealed partial class DotnetTestConnection : IInvocationLifetimeCoordinationChannel
{
    private static readonly Version InvocationLifetimeCoordinationProtocolVersion = new(1, 5, 0);

    private readonly TaskCompletionSource<InvocationCleanupRequest?> _invocationCleanupRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsInvocationLifetimeCoordinationAccepted { get; private set; }

    // Lifecycle messages are sent without the application token: they must still reach dotnet test after the test
    // session was canceled, because the SDK waits for them to release the other modules and finish the invocation.
    public async Task SendInvocationSetupCompletedAsync(bool succeeded, string? errorMessage, IReadOnlyList<InvocationLifetimeOutput> outputs)
        => await GetTransportClient().RequestReplyAsync<InvocationSetupCompleted, VoidResponse>(
            new InvocationSetupCompleted(GetExecutionId(), InstanceId, succeeded, errorMessage, [.. outputs]),
            CancellationToken.None).ConfigureAwait(false);

    public async Task SendModuleTestsCompletedAsync(int exitCode)
        => await GetTransportClient().RequestReplyAsync<ModuleTestsCompleted, VoidResponse>(
            new ModuleTestsCompleted(GetExecutionId(), InstanceId, exitCode),
            CancellationToken.None).ConfigureAwait(false);

    public async Task SendInvocationCleanupCompletedAsync(bool succeeded, string? errorMessage)
        => await GetTransportClient().RequestReplyAsync<InvocationCleanupCompleted, VoidResponse>(
            new InvocationCleanupCompleted(GetExecutionId(), InstanceId, succeeded, errorMessage),
            CancellationToken.None).ConfigureAwait(false);

    public async Task<InvocationCleanupRequest?> WaitForInvocationCleanupRequestAsync(CancellationToken cancellationToken)
    {
        if (!IsInvocationLifetimeCoordinationAccepted || _serverControlListenerTask is null)
        {
            // The cleanup request arrives only on the control channel of an accepted owner.
            return null;
        }

        return await _invocationCleanupRequest.Task.WithCancellationAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsInvocationLifetimeCoordinationAcknowledged(
        bool isCompatible,
        bool versionParsed,
        Version? negotiatedVersion,
        bool isServerControlChannelSupported,
        bool advertisedHandlers,
        IReadOnlyDictionary<byte, string>? responseProperties)
        => isCompatible
            && versionParsed
            && negotiatedVersion >= InvocationLifetimeCoordinationProtocolVersion
            && isServerControlChannelSupported
            && advertisedHandlers
            && responseProperties?.TryGetValue(HandshakeMessagePropertyNames.InvocationLifetimeCoordinationAccepted, out string? accepted) is true
            && bool.TryParse(accepted, out bool isAccepted)
            && isAccepted;

    private IClient GetTransportClient()
        => _transportClient ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

    private string? GetExecutionId()
        => _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID);
}
