// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

/// <summary>
/// Testhost-side extension that appends a line per test state transition to a "sequence file"
/// shared with the testhost controller. On a non-graceful exit (i.e. a crash), the controller
/// reads this file to surface the tests that were running at the time of the crash, mirroring
/// the per-test journaling that <c>--hangdump</c> performs over IPC (which is unavailable for
/// crashes because the testhost is dead).
/// </summary>
internal sealed class CrashDumpSequenceLogger : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    // File schema:
    //   # MTP CrashDump test sequence v1 (format: <event>\t<isoTimestamp>\t<uid>\t<displayName-or-state>)
    //   STARTED\t<iso8601>\t<uid>\t<displayName>
    //   ENDED\t<iso8601>\t<uid>\t<finalState>
    // Tab is used as a separator so display names and states (which may contain spaces, '|', ':',
    // etc.) survive a round-trip. Tabs themselves are extremely rare in test names; the controller
    // tolerates extra trailing tabs by joining all remaining fields back together.
    private const string FileHeader = "# MTP CrashDump test sequence v1 (format: <event>\\t<isoTimestamp>\\t<uid>\\t<displayName-or-state>)";

    internal const string StartedEvent = "STARTED";
    internal const string EndedEvent = "ENDED";

    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILogger<CrashDumpSequenceLogger> _logger;
    private readonly IOutputDevice _outputDevice;

    // SemaphoreSlim instead of a plain `lock` so we can `await` the write/flush calls inside the
    // critical section without blocking a thread on synchronous I/O.
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);

    private string? _sequenceFilePath;
    private StreamWriter? _writer;
    private bool _disposed;

    public CrashDumpSequenceLogger(
        IEnvironment environment,
        IClock clock,
        ILoggerFactory loggerFactory,
        IOutputDevice outputDevice)
    {
        _environment = environment;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<CrashDumpSequenceLogger>();
        _outputDevice = outputDevice;
    }

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(CrashDumpSequenceLogger);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => nameof(CrashDumpSequenceLogger);

    public string Description => "Records the start and end of each test to a sequence file that can be used to identify the tests that were running at the time of a crash.";

    public Task<bool> IsEnabledAsync()
    {
        // The env var is set by the controller's CrashDumpEnvironmentVariableProvider only when the
        // sequence feature is enabled, so its presence is the sole gate here. If it is missing, the
        // testhost was either not launched via the crashdump controller or sequence logging was
        // explicitly disabled with --crash-sequence off.
        _sequenceFilePath = _environment.GetEnvironmentVariable(CrashDumpEnvironmentVariableProvider.SequenceFileEnvironmentVariableName);
        return Task.FromResult(!RoslynString.IsNullOrEmpty(_sequenceFilePath));
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        ApplicationStateGuard.Ensure(_sequenceFilePath is not null);

        try
        {
            // Ensure the destination directory exists. The controller chose a path under the configured
            // results directory which is created elsewhere in the pipeline; this is a defensive call to
            // cope with users who customize the path via --crashdump-filename to a subdirectory.
            string? directory = Path.GetDirectoryName(_sequenceFilePath);
            if (!RoslynString.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Open in Create mode (overwrites any stale file from a previous run). AutoFlush flushes the
            // StreamWriter buffer into the FileStream after each Write*, and FileStream then forwards
            // those bytes to the OS — that is enough for a record to survive a process crash because
            // the OS still owns the cache after the testhost dies. We do not call fsync (Flush(true))
            // because a sequence file is not required to survive an OS-level crash or power loss.
            var fileStream = new FileStream(_sequenceFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };
            await _writer.WriteLineAsync(FileHeader).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (IsExpectedFileException(ex))
        {
            // The sequence file is a best-effort diagnostic. If we cannot open it (e.g. the disk is
            // full, ACLs deny write, the path is invalid, or any other filesystem-level error), we
            // surface the failure to the user via the output device (this is a one-time,
            // session-startup failure, so it is safe and useful to display it rather than only log
            // it) and behave as if the feature were disabled — failing the test run for this would
            // be worse than missing the diagnostic. The full exception (ex.ToString()) is included so
            // the root cause is not lost.
            try
            {
                await _outputDevice.DisplayAsync(
                    this,
                    new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileOpenError, _sequenceFilePath, ex)),
                    testSessionContext.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception outputException)
            {
                // Reporting a best-effort diagnostic must not turn its original file failure into a
                // session-start failure when the output transport is unavailable or being cancelled.
                await TryLogWarningAsync(
                    $"Failed to initialize crash sequence file '{_sequenceFilePath}': {ex}{Environment.NewLine}"
                    + $"Additionally, displaying this warning failed: {outputException}").ConfigureAwait(false);
            }
            finally
            {
                StreamWriter? writer = _writer;
                _writer = null;
                if (writer is not null)
                {
                    try
                    {
#if NETCOREAPP
                        await writer.DisposeAsync().ConfigureAwait(false);
#else
                        writer.Dispose();
#endif
                    }
                    catch (Exception cleanupException) when (IsExpectedFileException(cleanupException))
                    {
                        await TryLogWarningAsync($"Failed to close crash sequence file '{_sequenceFilePath}' after initialization failed: {cleanupException}").ConfigureAwait(false);
                    }
                }
            }
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (_writer is null || value is not TestNodeUpdateMessage update)
        {
            return;
        }

        TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null)
        {
            return;
        }

        string? line = state switch
        {
            InProgressTestNodeStateProperty => FormatLine(StartedEvent, _clock.UtcNow, update.TestNode.Uid, update.TestNode.DisplayName),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete - keep parity with HangDumpActivityIndicator's terminal-state set.
            PassedTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Passed"),
            FailedTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Failed"),
            ErrorTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Error"),
            SkippedTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Skipped"),
            CancelledTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Cancelled"),
            TimeoutTestNodeStateProperty => FormatLine(EndedEvent, _clock.UtcNow, update.TestNode.Uid, "Timeout"),
#pragma warning restore CS0618, MTP0001
            _ => null,
        };

        if (line is null)
        {
            return;
        }

        // ConsumeAsync may be invoked concurrently from multiple data producers/threads; serialize
        // writes to keep the on-disk record consistent. Writes are tiny (one line each) so contention
        // is negligible.
        await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Re-check under the semaphore to defend against a concurrent Dispose closing the writer
            // between the null check above and the write here.
            if (_writer is null)
            {
                return;
            }

            try
            {
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // Writer was disposed between the check above and the write; nothing more to do.
            }
            catch (IOException ex)
            {
                // Best-effort logging only: dropping a single record is better than failing the test
                // run. We deliberately keep this on the ILogger (rather than IOutputDevice, as we do
                // for the one-time open failure above) because ConsumeAsync runs once per test state
                // transition — surfacing every dropped write to the user's console could flood the
                // output for a long test run hitting a persistent I/O problem (e.g. a full disk). The
                // full exception (ex.ToString()) is preserved so the root cause is not lost even
                // though only the log file captures it.
                await TryLogWarningAsync(string.Format(CultureInfo.InvariantCulture, CrashDumpResources.CrashDumpSequenceFileWriteError, _sequenceFilePath, ex)).ConfigureAwait(false);
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        // Flush remaining bytes; the controller-side handler decides whether to publish (on crash) or
        // delete (on graceful exit) the file. We deliberately do not delete from here so that a crash
        // *during* finishing still leaves the sequence file behind for the controller to inspect.
        await _writeSemaphore.WaitAsync(testSessionContext.CancellationToken).ConfigureAwait(false);
        try
        {
            if (_writer is not null)
            {
                try
                {
                    await _writer.FlushAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    // Ignore - already disposed.
                }
                catch (IOException)
                {
                    // Best-effort - ignore final flush failure.
                }
            }
        }
        finally
        {
            _writeSemaphore.Release();
        }
    }

    internal static string FormatLine(string eventName, DateTimeOffset timestamp, TestNodeUid uid, string lastField)
        // Tab-separated. Replace tab/newline in user-controlled fields to keep the format unambiguous;
        // this is a diagnostic file, not user content, so silent normalization is acceptable.
        => string.Join(
            "\t",
            eventName,
            timestamp.ToString("O", CultureInfo.InvariantCulture),
            Sanitize(uid.Value),
            Sanitize(lastField));

    private static string Sanitize(string value)
        => value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static bool IsExpectedFileException(Exception ex)
        => ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException
            or DirectoryNotFoundException
            or System.Security.SecurityException;

    private async Task TryLogWarningAsync(string message)
    {
        try
        {
            await _logger.LogWarningAsync(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Fallback diagnostics must not make this best-effort extension fail the test session.
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _writeSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_writer is not null)
            {
                try
                {
                    await _writer.DisposeAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // Best-effort - ignore close failures.
                }

                _writer = null;
            }
        }
        finally
        {
            _writeSemaphore.Release();
            _writeSemaphore.Dispose();
        }
    }
#endif

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Synchronous fallback for non-NETCOREAPP targets and for callers that don't observe
        // IAsyncDisposable. We use the synchronous Wait() / Dispose() pair here intentionally:
        // there is no async context to await on, and the wait is bounded by the brief duration of
        // any concurrent ConsumeAsync write.
        // CA1416: SemaphoreSlim.Wait() is unsupported on 'browser'; this code path is unreachable on
        // browser because IsEnabledAsync returns false there (the controller-side env var provider
        // requires a NETCOREAPP runtime which excludes browser).
#pragma warning disable CA1416
        _writeSemaphore.Wait();
#pragma warning restore CA1416
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                _writer?.Dispose();
            }
            catch (IOException)
            {
                // Best-effort - ignore close failures.
            }

            _writer = null;
        }
        finally
        {
            _writeSemaphore.Release();
            _writeSemaphore.Dispose();
        }
    }
}
