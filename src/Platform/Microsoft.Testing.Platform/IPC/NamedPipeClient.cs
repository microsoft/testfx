// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
#if !MTP_MSBUILD_TASKS
[UnsupportedOSPlatform("browser")]
#endif
internal sealed class NamedPipeClient : NamedPipeBase, IClient
{
    private const PipeOptions AsyncCurrentUserPipeOptions = PipeOptions.Asynchronous
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;

    private readonly NamedPipeClientStream _namedPipeClientStream;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IEnvironment _environment;

    private bool _disposed;

    public NamedPipeClient(string name)
        : this(name, new SystemEnvironment())
    {
    }

    public NamedPipeClient(string name, IEnvironment environment)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        _namedPipeClientStream = new(".", name, PipeDirection.InOut, AsyncCurrentUserPipeOptions);
        PipeName = name;
        _environment = environment;
    }

    public string PipeName { get; }

    public bool IsConnected => _namedPipeClientStream.IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken)
        => await _namedPipeClientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

    public async Task<TResponse> RequestReplyAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
       where TRequest : IRequest
       where TResponse : IResponse
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            INamedPipeSerializer requestNamedPipeSerializer = GetSerializer(typeof(TRequest));

            // Serialize and send the request
            try
            {
                await WriteMessageAsync(_namedPipeClientStream, requestNamedPipeSerializer, request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The server disconnected while we were writing the request. Mirror the read-EOF handling
                // below: if we cannot deliver the request there's no way to recover, so exit abnormally
                // instead of surfacing a raw IPC error to the caller.
                _environment.Exit((int)ExitCode.GenericFailure);
                throw;
            }

            // Read the response
            object? response = await ReadNextMessageAsync(_namedPipeClientStream, cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                // We are reading a message response.
                // If we cannot get a response, there is no way we can recover and continue executing.
                // This can happen if the other processes gets killed or crashes while while it's sending the response.
                // This is especially important for 'dotnet test', where the user can simply kill the dotnet.exe process themselves.
                // In that case, we want the MTP process to also die.
                // Exit code 1 indicates abnormal termination due to IPC connection loss.

                // Surface a diagnostic on stderr so the user has a chance to understand why this process is exiting.
                // We deliberately use Console.Error (and not stdout) to avoid corrupting any machine-readable output
                // that may be flowing through stdout.
                try
                {
                    await Console.Error.WriteLineAsync($"[NamedPipeClient] Pipe '{PipeName}' was closed by the server before a response was received. The peer process likely exited or was killed. Terminating with exit code {(int)ExitCode.GenericFailure}.").ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or NotSupportedException or ArgumentException or OperationCanceledException)
                {
                    // Best-effort diagnostic only; never let logging failures shadow the original problem.
                }

                _environment.Exit((int)ExitCode.GenericFailure);

                // _environment.Exit normally terminates the process and never returns. Guard against
                // alternate IEnvironment implementations (e.g. tests) that don't terminate by throwing
                // explicitly — otherwise we would fall through to the cast below and return null.
                throw new InvalidOperationException($"Pipe '{PipeName}' was closed by the server before a response was received.");
            }

            return (TResponse)response!;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _lock.Dispose();
        DisposeBuffers();
        _namedPipeClientStream.Dispose();
        _disposed = true;
    }
}
