﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Polyfills;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryLifecycleCallbacks : ITestApplicationLifecycleCallbacks,
#if NETCOREAPP
    IAsyncDisposable
#else
    IDisposable
#endif
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommandLineOptions _commandLineOptions;

    public RetryLifecycleCallbacks(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _commandLineOptions = _serviceProvider.GetCommandLineOptions();
    }

    public NamedPipeClient? Client { get; private set; }

    public string[]? FailedTestsIDToRetry { get; private set; }

    public string Uid => nameof(RetryLifecycleCallbacks);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.RetryFailedTestsExtensionDisplayName;

    public string Description => ExtensionResources.RetryFailedTestsExtensionDescription;

    public async Task BeforeRunAsync(CancellationToken cancellationToken)
    {
        if (!_commandLineOptions.TryGetOptionArgumentList(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName, out string[]? pipeName))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        ILogger<RetryLifecycleCallbacks> logger = _serviceProvider.GetLoggerFactory().CreateLogger<RetryLifecycleCallbacks>();

        Guard.NotNull(pipeName);
        ArgumentGuard.Ensure(pipeName.Length == 1, nameof(pipeName), "Pipe name expected");
        logger.LogDebug($"Connecting to pipe '{pipeName[0]}'");

        Client = new(pipeName[0]);
        Client.RegisterSerializer<VoidResponseSerializer, VoidResponse>();
        Client.RegisterSerializer<FailedTestRequestSerializer, FailedTestRequest>();
        Client.RegisterSerializer<GetListOfFailedTestsRequestSerializer, GetListOfFailedTestsRequest>();
        Client.RegisterSerializer<GetListOfFailedTestsResponseSerializer, GetListOfFailedTestsResponse>();
        Client.RegisterSerializer<TotalTestsRunRequestSerializer, TotalTestsRunRequest>();
        await Client.ConnectAsync(cancellationToken);

        GetListOfFailedTestsResponse result = await Client.RequestReplyAsync<GetListOfFailedTestsRequest, GetListOfFailedTestsResponse>(new GetListOfFailedTestsRequest(), cancellationToken);
        FailedTestsIDToRetry = result.FailedTestIds;
    }

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(RetryCommandLineOptionsProvider.RetryFailedTestsPipeNameOptionName));

    public Task AfterRunAsync(int exitCode, CancellationToken cancellation)
        => Task.CompletedTask;

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }
    }
#else
    public void Dispose() => Client?.Dispose();
#endif
}
