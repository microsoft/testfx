// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Diagnostics;

/// <summary>
/// Testhost-side extension that appends a line per test state transition to a "sequence file"
/// shared with the testhost controller. On a non-graceful exit (i.e. a crash), the controller
/// reads this file to surface the tests that were running at the time of the crash, mirroring
/// the per-test journaling that <c>--hangdump</c> performs over IPC (which is unavailable for
/// crashes because the testhost is dead).
/// </summary>
internal sealed class CrashDumpSequenceLogger : IDataConsumer, ITestSessionLifetimeHandler,
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
    private readonly object _writeLock = new();

    private string? _sequenceFilePath;
    private StreamWriter? _writer;
    private bool _disposed;

    public CrashDumpSequenceLogger(
        IEnvironment environment,
        IClock clock,
        ILoggerFactory loggerFactory)
    {
        _environment = environment;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<CrashDumpSequenceLogger>();
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

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
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
            _writer.WriteLine(FileHeader);
            _writer.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException or DirectoryNotFoundException or System.Security.SecurityException)
        {
            // The sequence file is a best-effort diagnostic. If we cannot open it (e.g. the disk is
            // full, ACLs deny write, the path is invalid, or any other filesystem-level error), we
            // trace the failure and behave as if the feature were disabled — failing the test run
            // for this would be worse than missing the diagnostic.
            _logger.LogWarning($"Failed to open crash sequence file '{_sequenceFilePath}': {ex.Message}");
            _writer?.Dispose();
            _writer = null;
        }

        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (_writer is null || value is not TestNodeUpdateMessage update)
        {
            return Task.CompletedTask;
        }

        TestNodeStateProperty? state = update.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null)
        {
            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

        // ConsumeAsync may be invoked concurrently from multiple data producers/threads; serialize
        // writes to keep the on-disk record consistent. Writes are tiny (one line each) so contention
        // is negligible.
        lock (_writeLock)
        {
            // Re-check under the lock to defend against a concurrent Dispose closing the writer
            // between the null check above and the write here.
            if (_writer is null)
            {
                return Task.CompletedTask;
            }

            try
            {
                _writer.WriteLine(line);
            }
            catch (ObjectDisposedException)
            {
                // Writer was disposed between the check above and the write; nothing more to do.
            }
            catch (IOException ex)
            {
                // Best-effort logging only: dropping a single record is better than failing the test run.
                _logger.LogWarning($"Failed to write to crash sequence file '{_sequenceFilePath}': {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        // Flush remaining bytes; the controller-side handler decides whether to publish (on crash) or
        // delete (on graceful exit) the file. We deliberately do not delete from here so that a crash
        // *during* finishing still leaves the sequence file behind for the controller to inspect.
        lock (_writeLock)
        {
            try
            {
                _writer?.Flush();
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

        return Task.CompletedTask;
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

#if NETCOREAPP
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
#endif

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_writeLock)
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
    }
}
