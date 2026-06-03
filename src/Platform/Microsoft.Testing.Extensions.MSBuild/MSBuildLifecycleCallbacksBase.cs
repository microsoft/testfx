// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;

namespace Microsoft.Testing.Extensions.MSBuild;

[UnsupportedOSPlatform("browser")]
internal abstract class MSBuildLifecycleCallbacksBase
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;

    protected MSBuildLifecycleCallbacksBase(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions)
    {
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
    }

    public virtual string Version => ExtensionVersion.DefaultSemVer;

    public virtual string Description => Resources.ExtensionResources.MSBuildExtensionsDescription;

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(MSBuildConstants.MSBuildNodeOptionKey));

    public virtual Task AfterRunAsync(int exitCode, CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual void RegisterAdditionalSerializers(NamedPipeClient pipeClient)
    {
    }

    protected NamedPipeClient CreatePipeClient()
    {
        NamedPipeClient pipeClient = new(GetPipeName());
        pipeClient.RegisterSerializer(new ModuleInfoRequestSerializer(), typeof(ModuleInfoRequest));
        pipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        RegisterAdditionalSerializers(pipeClient);
        return pipeClient;
    }

    protected async Task ConnectAndSendModuleInfoAsync(NamedPipeClient pipeClient, CancellationToken cancellationToken)
    {
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

    private string GetPipeName()
    {
        string[]? msbuildInfo = _commandLineOptions.TryGetOptionArgumentList(MSBuildConstants.MSBuildNodeOptionKey, out string[]? optionValues)
            ? optionValues
            : throw new InvalidOperationException($"MSBuild pipe name not found in the command line, missing {MSBuildConstants.MSBuildNodeOptionKey}");

        return msbuildInfo is [string { Length: > 0 } pipeName]
            ? pipeName
            : throw new InvalidOperationException($"MSBuild pipe name not found in the command line, missing argument for {MSBuildConstants.MSBuildNodeOptionKey}");
    }
}
