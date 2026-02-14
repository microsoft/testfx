// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Framework.Adapter;
using Microsoft.Testing.Framework.Configurations;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

using IConfiguration = Microsoft.Testing.Platform.Configurations.IConfiguration;

namespace Microsoft.Testing.Framework;

internal sealed class TestFramework : IDisposable, ITestFramework
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly TestingFrameworkExtension _extension;
    private readonly CountdownEvent _incomingRequestCounter = new(1);
    private readonly TestFrameworkEngine _engine;
    private readonly List<string> _sessionWarningMessages = [];
    private readonly List<string> _sessionErrorMessages = [];
    private SessionUid? _sessionId;

    public TestFramework(TestFrameworkConfiguration testFrameworkConfiguration, ITestNodesBuilder[] testNodesBuilders, TestingFrameworkExtension extension,
        IClock clock, ITask task, IConfiguration configuration, ITestFrameworkCapabilities capabilities)
    {
        _extension = extension;
        _engine = new(testFrameworkConfiguration, testNodesBuilders, extension, capabilities, clock, task, configuration);
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
    public async Task<bool> IsEnabledAsync() => await _extension.IsEnabledAsync().ConfigureAwait(false);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
    {
        if (_sessionId is not null)
        {
            throw new InvalidOperationException("Session already created");
        }

        _sessionId = context.SessionUid;
        _sessionWarningMessages.Clear();
        _sessionErrorMessages.Clear();
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
            await _incomingRequestCounter.WaitAsync(context.CancellationToken).ConfigureAwait(false);

            if (_sessionErrorMessages.Count > 0)
            {
                StringBuilder errorBuilder = new();
                errorBuilder.AppendLine("Test session failed with the following errors:");
                for (int i = 0; i < _sessionErrorMessages.Count; i++)
                {
                    errorBuilder.Append("  - ").AppendLine(_sessionErrorMessages[i]);
                }

                sessionResult.ErrorMessage = errorBuilder.ToString();
            }

            if (_sessionWarningMessages.Count > 0)
            {
                StringBuilder errorBuilder = new();
                errorBuilder.AppendLine("Test session raised the following warnings:");
                for (int i = 0; i < _sessionWarningMessages.Count; i++)
                {
                    errorBuilder.Append("  - ").AppendLine(_sessionWarningMessages[i]);
                }

                sessionResult.WarningMessage = errorBuilder.ToString();
            }

            sessionResult.IsSuccess = _incomingRequestCounter.CurrentCount == 0 && _sessionErrorMessages.Count == 0;
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

            Result result = await _engine.ExecuteRequestAsync(testExecutionRequest, context.MessageBus, context.CancellationToken).ConfigureAwait(false);

            foreach (IReason reason in result.Reasons)
            {
                if (reason is IErrorReason errorReason)
                {
                    _sessionErrorMessages.Add(errorReason.Exception?.ToString() ?? errorReason.Message);
                }
                else if (reason is IWarningReason warningReason)
                {
                    _sessionWarningMessages.Add(warningReason.Message);
                }
            }
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
