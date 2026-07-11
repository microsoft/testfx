// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

using Microsoft.CodeAnalysis;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.IPC;

[Embedded]
#if !MTP_MSBUILD_TASKS
[UnsupportedOSPlatform("browser")]
#endif
internal sealed class NamedPipeServer : NamedPipeConnectionBase, IServer
{
    private const PipeOptions AsyncCurrentUserPipeOptions = PipeOptions.Asynchronous
#if NET
        | PipeOptions.CurrentUserOnly
#endif
        ;

    private static bool IsUnix => Path.DirectorySeparatorChar == '/';

    // Maximum length, in bytes, of the path stored in sockaddr_un.sun_path for a Unix domain socket.
    // The smallest limit across supported platforms is macOS' 104 bytes (Linux allows 108); we use the
    // smaller value minus one for the NUL terminator so the resolved path stays portable.
    internal const int MaxUnixDomainSocketPathLengthInBytes = 104 - 1;

    private readonly Func<IRequest, Task<IResponse>> _callback;
    private readonly IEnvironment _environment;
    private readonly NamedPipeServerStream _namedPipeServerStream;
    private readonly ILogger _logger;
    private readonly ITask _task;
    private readonly CancellationToken _cancellationToken;
    private Task? _loopTask;
    private bool _disposed;

    public NamedPipeServer(
        string name,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(GetPipeName(name), callback, environment, logger, task, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        CancellationToken cancellationToken)
        : this(pipeNameDescription, callback, environment, logger, task, maxNumberOfServerInstances: 1, cancellationToken)
    {
    }

    public NamedPipeServer(
        PipeNameDescription pipeNameDescription,
        Func<IRequest, Task<IResponse>> callback,
        IEnvironment environment,
        ILogger logger,
        ITask task,
        int maxNumberOfServerInstances,
        CancellationToken cancellationToken)
    {
        if (pipeNameDescription is null)
        {
            throw new ArgumentNullException(nameof(pipeNameDescription));
        }

        _namedPipeServerStream = new((PipeName = pipeNameDescription).Name, PipeDirection.InOut, maxNumberOfServerInstances, PipeTransmissionMode.Byte, AsyncCurrentUserPipeOptions);
        _callback = callback;
        _environment = environment;
        _logger = logger;
        _task = task;
        _cancellationToken = cancellationToken;
    }

    public PipeNameDescription PipeName { get; }

    public bool WasConnected { get; private set; }

    public async Task WaitConnectionAsync(CancellationToken cancellationToken)
    {
        // NOTE: _cancellationToken field is usually the "test session" cancellation token.
        // And cancellationToken parameter may have hang mitigating timeout.
        // The parameter should only be used for the call of WaitForConnectionAsync and Task.Run call.
        // NOTE: The cancellation token passed to Task.Run will only have effect before the task is started by runtime.
        // Once it starts, it won't be considered.
        // Then, for the internal loop, we should use _cancellationToken, because we don't know for how long the loop will run.
        // So what we pass to InternalLoopAsync shouldn't have any timeout (it's usually linked to Ctrl+C).
        await _logger.LogDebugAsync($"Waiting for connection for the pipe name {PipeName.Name}").ConfigureAwait(false);
        await _namedPipeServerStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        WasConnected = true;
        await _logger.LogDebugAsync($"Client connected to {PipeName.Name}").ConfigureAwait(false);
        _loopTask = _task.Run(
            async () =>
        {
            try
            {
                await InternalLoopAsync(_cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == _cancellationToken)
            {
                // We are being canceled, so we don't need to wait anymore
            }
            catch (Exception ex)
            {
                await _logger.LogErrorAsync($"Exception on pipe: {PipeName.Name}", ex).ConfigureAwait(false);
                _environment.FailFast($"[NamedPipeServer] Unhandled exception:{_environment.NewLine}{ex}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 4 bytes = message size
    /// ------- Payload -------
    /// 4 bytes = serializer id
    /// x bytes = object buffer.
    /// </summary>
    private async Task InternalLoopAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            // Read the next request; null means the client disconnected
            object? requestObject = await ReadNextMessageAsync(_namedPipeServerStream, cancellationToken).ConfigureAwait(false);
            if (requestObject is null)
            {
                await _logger.LogDebugAsync($"Client disconnected from pipe '{PipeName.Name}', exiting read loop").ConfigureAwait(false);
                return;
            }

            // Dispatch the request and obtain the response
            IResponse response = await _callback((IRequest)requestObject).ConfigureAwait(false);

            // Serialize and send the response
            INamedPipeSerializer responseNamedPipeSerializer = GetSerializer(response.GetType());
            bool clientDisconnected = false;
            try
            {
                await WriteMessageAsync(_namedPipeServerStream, responseNamedPipeSerializer, response, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The client disconnected while we were writing the reply. Treat it as a graceful disconnect
                // (symmetric with the read-side EOF handling above) so the server loop exits without crashing
                // the host.
                await _logger.LogDebugAsync($"Pipe {PipeName.Name} broken while writing reply; treating as client disconnect: {ex.Message}").ConfigureAwait(false);
                clientDisconnected = true;
            }

            if (clientDisconnected)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Computes the OS-level named pipe name for a friendly <paramref name="name"/>.
    /// </summary>
    /// <remarks>
    /// Invariant (important for cross-version / cross-repo compatibility): the process that <b>creates</b> the
    /// pipe resolves the directory locally and hands the <b>fully-resolved</b> path to the peer (via an
    /// environment variable, a command-line argument, or the dotnet-test handshake). Peers use that path verbatim
    /// and never recompute it. Do NOT turn the directory into a convention that both sides derive independently
    /// from a shared friendly name/env var - doing so would couple the SDK and test-host versions. Because only
    /// the creator's resolution is ever used, a difference in TESTINGPLATFORM_PIPE_DIRECTORY / TMPDIR between the
    /// two processes is harmless.
    /// </remarks>
    public static PipeNameDescription GetPipeName(string name)
    {
        if (!IsUnix)
        {
            return new PipeNameDescription($"testingplatform.pipe.{name.Replace('\\', '.')}");
        }

        // On Unix the named pipe is backed by a Unix domain socket file on disk. Historically this file
        // was always created under '/tmp' (similar to
        // https://github.com/dotnet/roslyn/blob/99bf83c7bc52fa1ff27cf792db38755d5767c004/src/Compilers/Shared/NamedPipeUtil.cs#L26-L42).
        // That is a problem in sandboxed environments that block '/tmp' (see
        // https://github.com/microsoft/testfx/issues/9821), so we allow the directory to be relocated.
        // Resolution precedence:
        //   1. TESTINGPLATFORM_PIPE_DIRECTORY   - explicit opt-in override, a guaranteed escape hatch.
        //   2. Path.GetTempPath()               - honors TMPDIR on Unix; usually a per-user, allowed dir.
        //   3. '/tmp'                           - preserves the previous default when neither is set.
        (string directory, bool isExplicitOverride) = ResolvePipeDirectory();

        // Normalize to an absolute path regardless of which precedence branch supplied the directory. An
        // explicit override or a relative TMPDIR can be relative, and on Unix NamedPipeServerStream only treats
        // rooted names as socket paths (it rejects separators in non-rooted names). The invariant also requires
        // handing peers a fully-resolved path. This additionally collapses any '..' segments. '/tmp' and an
        // already-absolute temp path are unchanged.
        directory = Path.GetFullPath(directory);

        // Only actively validate the explicit override: it is user-supplied and the most likely to be wrong
        // (typo, missing directory, wrong permissions), so we create it if needed and fail fast with an
        // actionable message. Path.GetTempPath()/'/tmp' are OS-managed and effectively always writable, so we
        // skip the probe there to avoid extra I/O and a behavior change on every pipe creation.
        if (isExplicitOverride)
        {
            EnsureDirectoryIsWritable(directory);
        }

        string path = Path.Combine(directory, name);
        EnsurePathLengthWithinLimit(path);

        return new PipeNameDescription(path);
    }

    private static (string Directory, bool IsExplicitOverride) ResolvePipeDirectory()
        => ResolvePipeDirectory(
            // Read the environment variable directly rather than through IEnvironment: GetPipeName is a static
            // method invoked before any NamedPipeServer instance (and its IEnvironment) exists, and this file is
            // shared-compiled into extension projects that do not link the SystemEnvironment wrapper. The banned
            // API is suppressed locally, mirroring how SystemEnvironment itself wraps Environment.
#pragma warning disable RS0030 // Do not use banned APIs
            Environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY),
#pragma warning restore RS0030 // Do not use banned APIs
            Path.GetTempPath());

    // Pure resolution logic split out so it can be unit-tested on any OS (the Unix branch of GetPipeName
    // never runs on Windows) without mutating process-wide environment variables.
    internal static (string Directory, bool IsExplicitOverride) ResolvePipeDirectory(string? overrideDirectory, string? tempPath)
    {
        if (!RoslynString.IsNullOrWhiteSpace(overrideDirectory))
        {
            return (overrideDirectory, true);
        }

        // Path.GetTempPath() honors TMPDIR on Unix and already falls back to '/tmp' itself when TMPDIR is unset,
        // and it always returns a non-empty string in practice. The explicit '/tmp' below is therefore a
        // defensive net that is only reachable when a caller passes null/empty tempPath (i.e. the test overload).
        return RoslynString.IsNullOrWhiteSpace(tempPath)
            ? ("/tmp", false)
            : (tempPath, false);
    }

    // Internal for unit testing: normalize/create the explicit override directory and verify it is writable,
    // failing fast with an actionable, localized message instead of a cryptic socket bind error later.
    internal static void EnsureDirectoryIsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            // Probe write access with a short-lived file so a misconfigured directory fails fast with an
            // actionable message instead of surfacing a cryptic socket bind failure later on.
            string probePath = Path.Combine(directory, $"testingplatform.probe.{Guid.NewGuid():N}");
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException or ArgumentException)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.NamedPipeDirectoryNotWritableErrorMessage,
                    directory,
                    ex.Message,
                    EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY),
                ex);
        }
    }

    // Internal for unit testing: enforce the Unix domain socket sun_path budget so a too-long directory
    // (e.g. a deep TMPDIR on macOS) fails with an actionable message instead of a cryptic bind error.
    internal static void EnsurePathLengthWithinLimit(string path)
    {
        int byteLength = System.Text.Encoding.UTF8.GetByteCount(path);
        if (byteLength > MaxUnixDomainSocketPathLengthInBytes)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.NamedPipePathTooLongErrorMessage,
                    path,
                    byteLength,
                    MaxUnixDomainSocketPathLengthInBytes,
                    EnvironmentVariableConstants.TESTINGPLATFORM_PIPE_DIRECTORY));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.
            ApplicationStateGuard.Ensure(_loopTask is not null);

            // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
            if (!_loopTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                _logger.LogError($"NamedPipeServer.Dispose: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={_loopTask.Status}.");
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();

        _disposed = true;
    }

#if NET
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (WasConnected)
        {
            // If the loop task is null at this point we have race condition, means that the task didn't start yet and we already dispose.
            // This is unexpected and we throw an exception.
            ApplicationStateGuard.Ensure(_loopTask is not null);

            try
            {
                // To close gracefully we need to ensure that the client closed the stream in the InternalLoopAsync method (there is comment `// The client has disconnected`).
                await _loopTask.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, _cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _logger.LogErrorAsync($"NamedPipeServer.DisposeAsync: '{nameof(InternalLoopAsync)}' for pipe '{PipeName.Name}' did not complete within {TimeoutHelper.DefaultHangTimeSpanTimeout}. WasConnected={WasConnected}, LoopTaskStatus={_loopTask.Status}.").ConfigureAwait(false);
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    PlatformResources.InternalLoopAsyncDidNotExitSuccessfullyErrorMessage,
                    nameof(InternalLoopAsync)));
            }
        }

        _namedPipeServerStream.Dispose();
        DisposeBuffers();

        _disposed = true;
    }
#endif
}
