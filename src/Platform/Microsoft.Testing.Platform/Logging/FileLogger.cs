// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#else
using System.Collections.Concurrent;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLogger : IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly FileLoggerOptions _options;
    private readonly LogLevel _logLevel;
    private readonly IClock _clock;
    private readonly IConsole _console;
    private readonly IFileStream _fileStream;
    private readonly StreamWriter _writer;
    private readonly Task? _logLoop;

#if NETCOREAPP
    private readonly Channel<string>? _channel;
#else
    private readonly BlockingCollection<string>? _asyncLogs;
#endif
    private bool _disposed;

    public FileLogger(
        FileLoggerOptions options,
        LogLevel logLevel,
        IClock clock,
        ITask task,
        IConsole console,
        IFileSystem fileSystem,
        IFileStreamFactory fileStreamFactory)
    {
        _options = options;
        _clock = clock;
        _logLevel = logLevel;
        _console = console;

        if (!_options.SyncFlush)
        {
#if NETCOREAPP
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                // We process only 1 data at a time
                SingleReader = true,

                // We don't know how many threads will call the Log method
                SingleWriter = false,

                // We want to unlink the caller from the consumer
                AllowSynchronousContinuations = false,
            });
#else
            _asyncLogs = [];
#endif

            _logLoop = task.Run(WriteLogToFileAsync, CancellationToken.None);
        }

        if (_options.FileName is not null)
        {
            string fileNameFullPath = Path.Combine(_options.LogFolder, _options.FileName);
            _fileStream = fileSystem.Exists(fileNameFullPath)
                ? OpenFileStreamForAppend(fileStreamFactory, fileNameFullPath)
                : CreateFileStream(fileStreamFactory, fileNameFullPath);
        }
        else
        {
            _fileStream = CreateFileStream(fileStreamFactory);
        }

        FileName = _fileStream.Name;

        // In case of malformed UTF8 characters we don't want to throw.
        _writer = new StreamWriter(
            _fileStream.Stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false))
        {
            AutoFlush = true,
        };
    }

    public string FileName { get; private set; }

    private static IFileStream OpenFileStreamForAppend(IFileStreamFactory fileStreamFactory, string fileName)
        => fileStreamFactory.Create(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);

    private IFileStream CreateFileStream(IFileStreamFactory fileStreamFactory, string? fileName = null)
    {
        if (fileName is not null)
        {
            return fileStreamFactory.Create(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }

        DateTimeOffset firstTryTime = _clock.UtcNow;
        while (true)
        {
            if (_clock.UtcNow - firstTryTime > TimeSpan.FromSeconds(3))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.CannotCreateUniqueLogFileErrorMessage, fileName));
            }

            try
            {
                fileName = $"{_options.LogPrefixName}_{_clock.UtcNow.ToString("MMddHHssfff", CultureInfo.InvariantCulture)}.diag";
                return fileStreamFactory.Create(Path.Combine(_options.LogFolder, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            }
            catch (IOException)
            {
                // In case of file with the same name we retry with a new name.
            }
        }
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (_options.SyncFlush)
        {
            InternalSyncLog(logLevel, state, exception, formatter, category);
        }
        else
        {
            EnqueueLog(logLevel, state, exception, formatter, category);
        }
    }

    private void InternalSyncLog<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (!_semaphore.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutAcquiringSemaphoreErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }

        try
        {
            _writer.WriteLine($"[{_clock.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {category} - {logLevel}] {formatter(state, exception)}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (_options.SyncFlush)
        {
            await InternalAsyncLogAsync(logLevel, state, exception, formatter, category);
        }
        else
        {
            EnqueueLog(logLevel, state, exception, formatter, category);
        }
    }

    private async Task InternalAsyncLogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        if (!await _semaphore.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutAcquiringSemaphoreErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }

        try
        {
            await _writer.WriteLineAsync(BuildLogEntry(logLevel, state, exception, formatter, category));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void EnqueueLog<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        EnsureAsyncLogObjectsAreNotNull();

        string log = BuildLogEntry(logLevel, state, exception, formatter, category);
#if NETCOREAPP
        if (!_channel.Writer.TryWrite(log))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.FailedToWriteLogToChannelErrorMessage, log));
        }
#else
        _asyncLogs.Add(log);
#endif
    }

    private string BuildLogEntry<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
        => $"{_clock.UtcNow:O} {category} {logLevel.ToString().ToUpper(CultureInfo.InvariantCulture)} {formatter(state, exception)}";

    private async Task WriteLogToFileAsync()
    {
        // We do this check out of the try because we want to crash the process if the _channel/_asyncLogs is null.
#if NETCOREAPP
        ApplicationStateGuard.Ensure(_channel is not null);
#else
        // We do this check out of the try because we want to crash the process if the _asyncLogs is null.
        ApplicationStateGuard.Ensure(_asyncLogs is not null);
#endif

        try
        {
#if NETCOREAPP
            // We don't need cancellation token because the task will be stopped when the Channel is completed thanks to the call to Complete() inside the Dispose method.
            while (await _channel.Reader.WaitToReadAsync())
            {
                await _writer.WriteLineAsync(await _channel.Reader.ReadAsync());
            }
#else
            // We don't need cancellation token because the task will be stopped when the BlockingCollection is completed thanks to the call to CompleteAdding()
            // inside the Dispose method.
            foreach (string message in _asyncLogs.GetConsumingEnumerable())
            {
                await _writer.WriteLineAsync(message);
            }
#endif
        }
        catch (Exception ex)
        {
            _console.WriteLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnexpectedExceptionInFileLoggerErrorMessage, ex));
        }
    }

#if NETCOREAPP
    [MemberNotNull(nameof(_channel), nameof(_logLoop))]
#else
    [MemberNotNull(nameof(_asyncLogs), nameof(_logLoop))]
#endif
    private void EnsureAsyncLogObjectsAreNotNull()
    {
#if NETCOREAPP
        ApplicationStateGuard.Ensure(_channel is not null);
#else
        ApplicationStateGuard.Ensure(_asyncLogs is not null);
#endif
        ApplicationStateGuard.Ensure(_logLoop is not null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_options.SyncFlush)
        {
            EnsureAsyncLogObjectsAreNotNull();

#if NETCOREAPP
            // Wait for all logs to be written
            _channel.Writer.TryComplete();
#else
            // Wait for all logs to be written
            _asyncLogs.CompleteAdding();
#endif

            if (!_logLoop.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutFlushingLogsErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
            }
        }

        _semaphore.Dispose();
        _writer.Flush();
        _writer.Dispose();
        _fileStream.Dispose();
        _disposed = true;
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!_options.SyncFlush)
        {
            EnsureAsyncLogObjectsAreNotNull();

            // Wait for all logs to be written
            _channel.Writer.TryComplete();
            await _logLoop.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        _semaphore.Dispose();
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
        await _fileStream.DisposeAsync();
        _disposed = true;
    }
#endif
}
