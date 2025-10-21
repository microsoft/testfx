// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal sealed class RetryFailedTestsPipeServer : IDisposable
{
    private readonly NamedPipeServer _singleConnectionNamedPipeServer;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly string[] _failedTests;

    public RetryFailedTestsPipeServer(IServiceProvider serviceProvider, string[] failedTests, ILogger logger)
    {
        _pipeNameDescription = NamedPipeServer.GetPipeName(Guid.NewGuid().ToString("N"), serviceProvider.GetEnvironment());
        logger.LogTrace($"Retry server pipe name: '{_pipeNameDescription.Name}'");
        _singleConnectionNamedPipeServer = new NamedPipeServer(_pipeNameDescription, CallbackAsync,
            serviceProvider.GetEnvironment(),
            serviceProvider.GetLoggerFactory().CreateLogger<RetryFailedTestsPipeServer>(),
            serviceProvider.GetTask(),
            serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken);

        _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new FailedTestRequestSerializer(), typeof(FailedTestRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new GetListOfFailedTestsRequestSerializer(), typeof(GetListOfFailedTestsRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new GetListOfFailedTestsResponseSerializer(), typeof(GetListOfFailedTestsResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new TotalTestsRunRequestSerializer(), typeof(TotalTestsRunRequest));
        _failedTests = failedTests;
    }

    public string PipeName => _pipeNameDescription.Name;

    public List<string>? FailedUID { get; private set; }

    public int TotalTestRan { get; private set; }

    public Task WaitForConnectionAsync(CancellationToken cancellationToken)
        => _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken);

    public void Dispose()
        => _singleConnectionNamedPipeServer.Dispose();

    private Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is FailedTestRequest failed)
        {
            FailedUID ??= [];
            FailedUID.Add(failed.Uid);
            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        if (request is GetListOfFailedTestsRequest)
        {
            return Task.FromResult((IResponse)new GetListOfFailedTestsResponse(_failedTests));
        }

        if (request is TotalTestsRunRequest totalTestsRunRequest)
        {
            TotalTestRan = totalTestsRunRequest.TotalTests;
            return Task.FromResult((IResponse)VoidResponse.CachedInstance);
        }

        throw ApplicationStateGuard.Unreachable();
    }
}
