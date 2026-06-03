// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.MSBuild;

[UnsupportedOSPlatform("browser")]
internal sealed class MSBuildOrchestratorLifetime : MSBuildLifecycleCallbacksBase, ITestHostOrchestratorApplicationLifetime
{
    public MSBuildOrchestratorLifetime(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions)
        : base(configuration, commandLineOptions)
    {
    }

    public string Uid => nameof(MSBuildOrchestratorLifetime);

    public string DisplayName => nameof(MSBuildOrchestratorLifetime);

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        using NamedPipeClient pipeClient = CreatePipeClient();
        await ConnectAndSendModuleInfoAsync(pipeClient, cancellationToken).ConfigureAwait(false);
    }
}
