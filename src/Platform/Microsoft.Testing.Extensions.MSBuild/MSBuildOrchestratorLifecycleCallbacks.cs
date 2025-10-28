// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using EasyNamedPipes.GeneratedSerializers.MSBuildProtocol;
using EasyNamedPipes.GeneratedSerializers.TestHostProtocol;

using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;

namespace Microsoft.Testing.Extensions.MSBuild;

internal sealed class MSBuildOrchestratorLifetime : ITestHostOrchestratorApplicationLifetime
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;

    public MSBuildOrchestratorLifetime(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions)
    {
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
    }

    public string Uid => nameof(MSBuildOrchestratorLifetime);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(MSBuildOrchestratorLifetime);

    public string Description => Resources.ExtensionResources.MSBuildExtensionsDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(MSBuildConstants.MSBuildNodeOptionKey));

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        if (!_commandLineOptions.TryGetOptionArgumentList(MSBuildConstants.MSBuildNodeOptionKey, out string[]? msbuildInfo))
        {
            throw new InvalidOperationException($"MSBuild pipe name not found in the command line, missing {MSBuildConstants.MSBuildNodeOptionKey}");
        }

        if (msbuildInfo is null || msbuildInfo.Length != 1 || string.IsNullOrEmpty(msbuildInfo[0]))
        {
            throw new InvalidOperationException($"MSBuild pipe name not found in the command line, missing argument for {MSBuildConstants.MSBuildNodeOptionKey}");
        }

        using var pipeClient = new NamedPipeClient(msbuildInfo[0]);
        pipeClient.RegisterSerializer(ModuleInfoRequestSerializer.Instance, typeof(ModuleInfoRequest));
        pipeClient.RegisterSerializer(VoidResponseSerializer.Instance, typeof(VoidResponse));
        using var cancellationTokenSource = new CancellationTokenSource(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
        await pipeClient.ConnectAsync(linkedCancellationToken.Token).ConfigureAwait(false);
        await pipeClient.RequestReplyAsync<ModuleInfoRequest, VoidResponse>(
            new ModuleInfoRequest(
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _configuration.GetTestResultDirectory()),
            cancellationToken).ConfigureAwait(false);
    }

    public Task AfterRunAsync(int exitCode, CancellationToken cancellation) => Task.CompletedTask;
}
