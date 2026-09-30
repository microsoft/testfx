// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerProvider(
    FileLoggerOptions options,
    LogLevel logLevel,
    bool customDirectory,
    IClock clock,
    ITask task,
    IConsole console,
    IFileSystem fileSystem,
    IFileStreamFactory fileStreamFactory)
    : IFileLoggerProvider, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
#if NET9_0_OR_GREATER
    private readonly Lock _fileLoggerLock = new();
#else
    private readonly object _fileLoggerLock = new();
#endif
    private readonly SemaphoreSlim _relocationSemaphore = new(1, 1);
    private readonly FileLoggerOptions _options = options;
    private readonly IClock _clock = clock;
    private readonly ITask _task = task;
    private readonly IConsole _console = console;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly bool _customDirectory = customDirectory;
    private readonly IFileStreamFactory _fileStreamFactory = fileStreamFactory;
    private Queue<Action<FileLogger>>? _pendingAsyncLogs;

    public LogLevel LogLevel { get; } = logLevel;

    public FileLogger FileLogger { get; private set; } = new(
        options,
        logLevel,
        clock,
        task,
        console,
        fileSystem,
        fileStreamFactory);

    public bool SyncFlush => _options.SyncFlush;

    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync(string testResultDirectory)
    {
        await _relocationSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            FileLogger previousLogger;
            string fileName;
            string previousFileName;
            lock (_fileLoggerLock)
            {
                // If custom directory is provided for the log file, we don't WANT to move the log file
                // We won't betray the users expectations.
                if (_customDirectory
                    || testResultDirectory == Path.GetDirectoryName(FileLogger.FileName))
                {
                    return;
                }

                previousLogger = FileLogger;
                fileName = Path.GetFileName(previousLogger.FileName);
                previousFileName = previousLogger.FileName;
                if (!_options.SyncFlush)
                {
                    _pendingAsyncLogs = new();
                }
            }

            await DisposeHelper.DisposeAsync(previousLogger).ConfigureAwait(false);

            // If disposal completed cleanly, relocate the log file into the test result directory. If a flush timed out,
            // the previous consumer loop may still own the file handle (the stream was opened with FileShare.Read, so a
            // move would fail on Windows) — in that case we leave the old file in place and skip the move rather than
            // turning a non-fatal flush timeout into a fatal IOException. See https://github.com/dotnet/sdk/issues/55215.
            if (previousLogger.IsFileHandleReleased)
            {
                _fileSystem.MoveFile(previousFileName, Path.Combine(testResultDirectory, fileName));
            }

            // Always install a fresh logger pointing at the test result directory so subsequent diagnostics keep working.
            // The previous instance's channel is completed, so writing to it would throw; replacing it here keeps logging
            // alive even on the degenerate timeout path (the old loop/handle are reclaimed at process exit).
            FileLogger replacementLogger = new(
                new FileLoggerOptions(testResultDirectory, _options.LogPrefixName, fileName, _options.SyncFlush),
                LogLevel,
                _clock,
                _task,
                _console,
                _fileSystem,
                _fileStreamFactory);

            lock (_fileLoggerLock)
            {
                FileLogger = replacementLogger;
                if (_pendingAsyncLogs is not null)
                {
                    while (_pendingAsyncLogs.Count > 0)
                    {
                        Action<FileLogger> pendingLog = _pendingAsyncLogs.Dequeue();
                        pendingLog(replacementLogger);
                    }

                    _pendingAsyncLogs = null;
                }
            }
        }
        finally
        {
            lock (_fileLoggerLock)
            {
                _pendingAsyncLogs = null;
            }

            _relocationSemaphore.Release();
        }
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLoggerCategory(this, categoryName);

    internal FileInfo GetLogFile()
    {
        lock (_fileLoggerLock)
        {
            return new(FileLogger.FileName);
        }
    }

    internal bool IsEnabled(LogLevel logLevel) => FileLogger.IsEnabled(logLevel);

    internal void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (_options.SyncFlush)
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException(PlatformResources.SyncFlushNotSupportedInBrowserErrorMessage);
            }

            _relocationSemaphore.Wait();
            try
            {
                FileLogger.Log(logLevel, state, exception, formatter, category);
            }
            finally
            {
                _relocationSemaphore.Release();
            }

            return;
        }

        lock (_fileLoggerLock)
        {
            if (_pendingAsyncLogs is null)
            {
                FileLogger.Log(logLevel, state, exception, formatter, category);
            }
            else if (FileLogger.IsEnabled(logLevel))
            {
                string message = formatter(state, exception);
                _pendingAsyncLogs.Enqueue(logger => logger.Log(logLevel, message, null, LoggingExtensions.Formatter, category));
            }
        }
    }

    internal Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (!_options.SyncFlush)
        {
            Log(logLevel, state, exception, formatter, category);
            return Task.CompletedTask;
        }

        return LogSynchronouslyAsync(logLevel, state, exception, formatter, category);
    }

    private async Task LogSynchronouslyAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        await _relocationSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await FileLogger.LogAsync(logLevel, state, exception, formatter, category).ConfigureAwait(false);
        }
        finally
        {
            _relocationSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsBrowser())
        {
            FileLogger.Dispose();
            return;
        }

        _relocationSemaphore.Wait();
        try
        {
            FileLogger.Dispose();
        }
        finally
        {
            _relocationSemaphore.Release();
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await _relocationSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await FileLogger.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _relocationSemaphore.Release();
        }
    }
#endif
}
