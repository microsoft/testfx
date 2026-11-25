// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform;

internal sealed partial class DotnetTestConnection
{
    /// <summary>
    /// Opens the reverse "server control" pipe the SDK advertised during the handshake and parks a long-poll
    /// <see cref="WaitForServerControlRequest"/> on it. The SDK completes that request with a
    /// <see cref="ServerControlMessage"/> whenever it wants to signal the test host (today only
    /// <see cref="ServerControlKinds.CancelSession"/>). On cancel - or when the control pipe drops, which means
    /// the host went away - <paramref name="onCancelSessionRequestedAsync"/> is invoked exactly once so the caller
    /// can stop the run cooperatively (preferring a graceful stop so trx/artifacts are still produced).
    /// </summary>
    /// <param name="onCancelSessionRequestedAsync">
    /// The reaction to a server-initiated cancel. It receives the test application cancellation token.
    /// </param>
    /// <remarks>
    /// The long-poll runs on a background task. An accepted owner waits for the connection before starting setup;
    /// other runs retain best-effort, non-blocking control-channel initialization. Callers should invoke this after
    /// a successful handshake and when <see cref="IsServerControlChannelSupported"/> is <see langword="true"/>. It is invoked
    /// once per connection on the awaited run path (before the test run starts), so the control-channel fields
    /// below are written here and only read later during exit/dispose - the single-threaded lifecycle makes the
    /// plain fields safe without extra synchronization.
    /// </remarks>
    public Task StartServerControlChannelAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync)
    {
        if (_serverControlPipeClient is not null)
        {
            return _serverControlReadyTask ?? Task.CompletedTask;
        }

        if (_transportKind != DotnetTestTransportKind.NamedPipe
            || OperatingSystem.IsBrowser()
            || OperatingSystem.IsWasi()
            || !IsServerControlChannelSupported
            || RoslynString.IsNullOrEmpty(_serverControlPipeName))
        {
            return IsInvocationLifetimeCoordinationAccepted
                ? Task.FromException(new InvalidOperationException("The coordinated owner requires a server control pipe."))
                : Task.CompletedTask;
        }

        // An accepted invocation-lifetime owner must keep listening after the test session was canceled, because
        // dotnet test delivers RunInvocationCleanup on this channel once every module completed. OnExitAsync and
        // Dispose cancel the listener explicitly, so it does not need to be linked to the application token then.
        _serverControlListenerCts = IsInvocationLifetimeCoordinationAccepted
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.CancellationToken);

        // exitProcessOnConnectionLoss: false - a dropped control pipe must not kill the test host; the listener
        // turns it into a cooperative cancel instead.
        var controlClient = new NamedPipeClient(_serverControlPipeName, _environment, exitProcessOnConnectionLoss: false);
        controlClient.RegisterAllSerializers();
        _serverControlPipeClient = controlClient;

        TaskCompletionSource<bool>? ready = IsInvocationLifetimeCoordinationAccepted
            ? new(TaskCreationOptions.RunContinuationsAsynchronously)
            : null;
        _serverControlReadyTask = ready?.Task;
#pragma warning disable CA1416 // The runtime guards above exclude browser before the named-pipe operation is scheduled.
        _serverControlListenerTask = Task.Run(
            () => ConnectAndListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, ready, _serverControlListenerCts.Token));
#pragma warning restore CA1416
        return _serverControlReadyTask ?? Task.CompletedTask;
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private async Task ConnectAndListenForServerControlAsync(NamedPipeClient controlClient, Func<CancellationToken, Task> onCancelSessionRequestedAsync, TaskCompletionSource<bool>? ready, CancellationToken cancellationToken)
    {
        try
        {
            // Bound the connect so a misbehaving SDK that advertises a pipe it never listens on cannot leave this
            // task parked forever holding resources.
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(30));
            await controlClient.ConnectAsync(connectCts.Token).ConfigureAwait(false);
            ready?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            // An accepted owner must fail before setup if it cannot receive the eventual cleanup request.
            ready?.TrySetException(ex);
            string consequence = ready is null ? "server-initiated cancellation is unavailable" : "coordinated setup cannot start";
            await TryLogAsync(LogLevel.Debug, $"Failed to connect to the server control pipe '{_serverControlPipeName}'; {consequence}: {ex}").ConfigureAwait(false);
            _invocationCleanupRequest.TrySetResult(null);
            return;
        }

        await TryLogAsync(LogLevel.Debug, $"Connected to the server control pipe '{_serverControlPipeName}'.").ConfigureAwait(false);

        try
        {
            await ListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Once the listener stopped, the invocation cleanup request can no longer arrive.
            _invocationCleanupRequest.TrySetResult(null);
        }
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private async Task ListenForServerControlAsync(NamedPipeClient controlClient, Func<CancellationToken, Task> onCancelSessionRequestedAsync, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ServerControlMessage message = await controlClient.RequestReplyAsync<WaitForServerControlRequest, ServerControlMessage>(
                    WaitForServerControlRequest.CachedInstance, cancellationToken).ConfigureAwait(false);

                if (message.Kind == ServerControlKinds.CancelSession)
                {
                    await RequestControlCancellationAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
                    if (!IsInvocationLifetimeCoordinationAccepted)
                    {
                        return;
                    }

                    // The accepted owner keeps parking: dotnet test still has to send RunInvocationCleanup.
                    continue;
                }

                if (message.Kind == ServerControlKinds.RunInvocationCleanup && IsInvocationLifetimeCoordinationAccepted)
                {
                    // A missing aggregate exit code is treated as a failure rather than silently as success.
                    _invocationCleanupRequest.TrySetResult(new InvocationCleanupRequest(message.AggregateExitCode ?? (int)ExitCode.GenericFailure, message.IsCanceled ?? false));
                    return;
                }

                // Unknown control kind (forward-compat): ignore it and keep parking for the next signal.
            }
        }
        catch (OperationCanceledException)
        {
            // We are shutting down (dispose or app cancellation) - nothing to do. Expected shutdown, so this stays
            // at Trace to avoid noise.
            await TryLogAsync(LogLevel.Trace, $"Server control pipe '{_serverControlPipeName}' listener stopped: shutdown requested.").ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The control pipe dropped while the session was still live => the host went away. Treat it as a
            // cooperative cancel so we still try to wind down and report whatever completed. NOTE: this makes it a
            // protocol requirement that the SDK keep the control pipe open until the data session ends - an early
            // close for any reason is interpreted here as a cancel.
            await RequestControlCancellationAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
            await TryLogAsync(LogLevel.Debug, $"Server control pipe '{_serverControlPipeName}' listener failed while the session was live; ensuring cooperative cancellation: {ex}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cancellation raced with a pipe error (e.g. the stream was disposed during teardown); ignore. This is
            // an expected shutdown race, so it stays at Trace.
            await TryLogAsync(LogLevel.Trace, $"Server control pipe '{_serverControlPipeName}' listener observed a race between shutdown and a pipe error during teardown: {ex}").ConfigureAwait(false);
        }
    }

    private async Task RequestCancelOnceAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync)
    {
        if (Interlocked.Exchange(ref _cancelRequested, 1) != 0)
        {
            return;
        }

        await onCancelSessionRequestedAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
    }

    private async Task RequestControlCancellationAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync)
    {
        try
        {
            await RequestCancelOnceAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsInvocationLifetimeCoordinationAccepted && IsNonFatalControlException(ex))
        {
            await TryLogAsync(LogLevel.Error, $"Server control cancellation callback failed: {ex}").ConfigureAwait(false);
            try
            {
                _cancellationTokenSource.Cancel();
            }
            catch (Exception cancelException) when (IsNonFatalControlException(cancelException))
            {
                await TryLogAsync(LogLevel.Error, $"Fallback application cancellation failed: {cancelException}").ConfigureAwait(false);
            }
        }
    }

    private static bool IsNonFatalControlException(Exception exception)
        => exception is AggregateException aggregateException
            ? aggregateException.Flatten().InnerExceptions.All(IsNonFatalControlException)
            : exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException
                and not AppDomainUnloadedException
                and not BadImageFormatException;
}
