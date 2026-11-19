// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Platform;

internal sealed partial class DotnetTestConnection
{
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    internal static async Task SendFileArtifactsToPipeAsync(
        string pipeName,
        FileArtifactMessages message,
        IEnvironment environment,
        CancellationToken cancellationToken)
    {
        // The activated host owns the SDK handshake; replay its envelopes without announcing the controller's runtime.
        using var client = new NamedPipeClient(pipeName, environment);
        client.RegisterAllSerializers();
        await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        await client.RequestReplyAsync<FileArtifactMessages, VoidResponse>(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendMessageAsync(IRequest message)
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        switch (message)
        {
            case DiscoveredTestMessages discoveredTestMessages:
                await transportClient.RequestReplyAsync<DiscoveredTestMessages, VoidResponse>(discoveredTestMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case TestResultMessages testResultMessages:
                await transportClient.RequestReplyAsync<TestResultMessages, VoidResponse>(testResultMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case FileArtifactMessages fileArtifactMessages:
                if (!IsCoverageSummarySupported && fileArtifactMessages.FileArtifacts.Any(artifact => artifact.CoverageSummary is not null))
                {
                    fileArtifactMessages = fileArtifactMessages with
                    {
                        FileArtifacts = [.. fileArtifactMessages.FileArtifacts.Select(artifact => artifact with { CoverageSummary = null })],
                    };
                }

                await transportClient.RequestReplyAsync<FileArtifactMessages, VoidResponse>(fileArtifactMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case CoverageSummaryMessages coverageSummaryMessages when IsCoverageSummarySupported:
                await transportClient.RequestReplyAsync<CoverageSummaryMessages, VoidResponse>(coverageSummaryMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case TestSessionEvent testSessionEvent:
                await transportClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(testSessionEvent, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case AzureDevOpsLogMessage azureDevOpsLogMessage:
                await transportClient.RequestReplyAsync<AzureDevOpsLogMessage, VoidResponse>(azureDevOpsLogMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case DisplayMessage displayMessage:
                await transportClient.RequestReplyAsync<DisplayMessage, VoidResponse>(displayMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;
        }
    }
}
