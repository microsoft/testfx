// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.MSBuild;

internal sealed class MSBuildTestApplicationLifecycleCallbacks : ITestHostApplicationLifetime, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;

    public MSBuildTestApplicationLifecycleCallbacks(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptions,
        ITestApplicationCancellationTokenSource testApplicationCancellationTokenSource)
    {
        _configuration = configuration;
        _commandLineOptions = commandLineOptions;
        _testApplicationCancellationTokenSource = testApplicationCancellationTokenSource;
    }

    public NamedPipeClient? PipeClient { get; private set; }

    public string Uid => nameof(MSBuildTestApplicationLifecycleCallbacks);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(MSBuildTestApplicationLifecycleCallbacks);

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

        PipeClient = new(msbuildInfo[0]);
        PipeClient.RegisterSerializer(new ModuleInfoRequestSerializer(), typeof(ModuleInfoRequest));
        PipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        PipeClient.RegisterSerializer(new FailedTestInfoRequestSerializer(), typeof(FailedTestInfoRequest));
        PipeClient.RegisterSerializer(new RunSummaryInfoRequestSerializer(), typeof(RunSummaryInfoRequest));
        using var cancellationTokenSource = new CancellationTokenSource(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, _testApplicationCancellationTokenSource.CancellationToken);
        await PipeClient.ConnectAsync(linkedCancellationToken.Token).ConfigureAwait(false);
        await PipeClient.RequestReplyAsync<ModuleInfoRequest, VoidResponse>(
            new ModuleInfoRequest(
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _configuration.GetTestResultDirectory()),
            _testApplicationCancellationTokenSource.CancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
        => PipeClient?.Dispose();

    public Task AfterRunAsync(int exitCode, CancellationToken cancellation) => Task.CompletedTask;
}
