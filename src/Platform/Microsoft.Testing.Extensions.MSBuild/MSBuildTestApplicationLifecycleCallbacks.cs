// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.IPC;

namespace Microsoft.Testing.Extensions.MSBuild;

[UnsupportedOSPlatform("browser")]
internal sealed class MSBuildTestApplicationLifecycleCallbacks : MSBuildLifecycleCallbacksBase, ITestHostApplicationLifetime, IDisposable
{
    public MSBuildTestApplicationLifecycleCallbacks(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions)
        : base(configuration, commandLineOptions)
    {
    }

    public NamedPipeClient? PipeClient { get; private set; }

    public string Uid => nameof(MSBuildTestApplicationLifecycleCallbacks);

    public string DisplayName => nameof(MSBuildTestApplicationLifecycleCallbacks);

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        PipeClient = CreatePipeClient();
        await ConnectAndSendModuleInfoAsync(PipeClient, cancellationToken).ConfigureAwait(false);
    }

    protected override void RegisterAdditionalSerializers(NamedPipeClient pipeClient)
    {
        pipeClient.RegisterSerializer(new FailedTestInfoRequestSerializer(), typeof(FailedTestInfoRequest));
        pipeClient.RegisterSerializer(new RunSummaryInfoRequestSerializer(), typeof(RunSummaryInfoRequest));
    }

    public void Dispose()
        => PipeClient?.Dispose();
}
