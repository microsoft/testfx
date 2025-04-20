// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace TestFramework.ForTestingMSTest;

internal sealed class TestFramework : IDisposable, ITestFramework
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly TestFrameworkExtension _extension;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private readonly TestFrameworkEngine _engine;
    private SessionUid? _sessionId;

    public TestFramework(TestFrameworkExtension extension, ILoggerFactory loggerFactory)
    {
        _extension = extension;
        _engine = new(extension, loggerFactory);
    }

    /// <inheritdoc />
    public string Uid => _extension.Uid;

    /// <inheritdoc />
    public string Version => _extension.Version;

    /// <inheritdoc />
    public string DisplayName => _extension.DisplayName;

    /// <inheritdoc />
    public string Description => _extension.Description;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync();

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        if (_sessionId is not null)
        {
            throw new InvalidOperationException("Session already created");
        }

        _sessionId = context.SessionUid;
        return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
    }

    public async Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        _sessionId = null;
        CloseTestSessionResult sessionResult = new();

        try
        {
            // Ensure we have finished processing all requests.
            _incomingRequestCounter.Signal();
            await _incomingRequestCounter.WaitAsync(context.CancellationToken);

            sessionResult.IsSuccess = _incomingRequestCounter.CurrentCount == 0;
            return sessionResult;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == context.CancellationToken)
        {
            // We are being cancelled, so we don't need to wait anymore
            sessionResult.WarningMessage +=
                (sessionResult.WarningMessage?.Length > 0 ? Environment.NewLine : string.Empty)
                + "Closing the test session was cancelled.";
            sessionResult.IsSuccess = false;
            return sessionResult;
        }
    }

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        _incomingRequestCounter.AddCount();
        try
        {
            if (context.Request is not TestExecutionRequest testExecutionRequest)
            {
                throw new InvalidOperationException($"Request type '{context.Request.GetType().FullName}' is not supported");
            }

            await _engine.ExecuteRequestAsync(testExecutionRequest, context.MessageBus, context.CancellationToken);
        }
        finally
        {
            _incomingRequestCounter.Signal();
            context.Complete();
        }
    }

    public void Dispose() => _incomingRequestCounter.Dispose();

#if NETCOREAPP

    public ValueTask DisposeAsync()
    {
        _incomingRequestCounter.Dispose();
        return default;
    }

#endif
}
