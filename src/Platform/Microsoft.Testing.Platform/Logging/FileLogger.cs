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
    private readonly string _logFolder;
    private readonly IClock _clock;
    private readonly LogLevel _logLevel;
    private readonly string _logPrefixName;
    private readonly bool _syncFlush;
    private readonly IConsole _console;
    private readonly FileStream _fileStream;
    private readonly StreamWriter _writer;
    private readonly Task? _logLoop;
#if NETCOREAPP
    private readonly Channel<string>? _channel;
#else
    private readonly BlockingCollection<string>? _asyncLogs;
#endif
    private bool _disposed;

    public FileLogger(string logFolder, string? fileName, LogLevel logLevel, string logPrefixName, bool syncFlush, IClock clock, ITask task, IConsole console)
    {
        _logFolder = logFolder;
        _clock = clock;
        _logLevel = logLevel;
        _logPrefixName = logPrefixName;
        _syncFlush = syncFlush;
        _console = console;

        if (!_syncFlush)
        {
#if NETCOREAPP
            _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions()
            {
                // We process only 1 data at a time
                SingleReader = true,

                // We don't know how many threads will call the Log method
                SingleWriter = false,

                // We want to unlink the caller from the consumer
                AllowSynchronousContinuations = false,
            });

            _logLoop = task.Run(WriteLogToFileAsync, CancellationToken.None);
#else
            _asyncLogs = [];
            _logLoop = task.Run(WriteLogToFileAsync, CancellationToken.None);
#endif
        }

        if (fileName is not null)
        {
            string fileNameFullPath = Path.Combine(logFolder, fileName);
            _fileStream = File.Exists(fileNameFullPath)
                ? OpenFileStreamForAppend(Path.Combine(logFolder, fileName))
                : CreateFileStream(Path.Combine(logFolder, fileName));
        }
        else
        {
            _fileStream = CreateFileStream();
        }

        FileName = _fileStream.Name;

        // In case of malformed UTF8 characters we don't want to throw.
        _writer = new StreamWriter(_fileStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false))
        {
            AutoFlush = true,
        };
    }

    public string FileName { get; private set; }

    private static FileStream OpenFileStreamForAppend(string fileName)
        => new(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);

    private FileStream CreateFileStream(string? fileName = null)
    {
        if (fileName is not null)
        {
            return new(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
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
                fileName = $"{_logPrefixName}_{_clock.UtcNow.ToString("MMddHHssfff", CultureInfo.InvariantCulture)}.diag";
                return new(Path.Combine(_logFolder, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
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
        if (_syncFlush)
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
        if (_syncFlush)
        {
            await InternalAsyncLogAsync(logLevel, state, exception, formatter, category);
        }
        else
        {
            EnqueueLog(logLevel, state, exception, formatter, category);
        }
    }

    public async Task InternalAsyncLogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
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
            await _writer.WriteLineAsync($"[{_clock.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {category} - {logLevel}] {formatter(state, exception)}");
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

        string log = $"[{_clock.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {category} - {logLevel}] {formatter(state, exception)}";
#if NETCOREAPP
        if (!_channel.Writer.TryWrite(log))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.FailedToWriteLogToChannelErrorMessage, log));
        }
#else
        _asyncLogs.Add(log);
#endif
    }

    private async Task WriteLogToFileAsync()
    {
#if NETCOREAPP
        // We do this check out of the try because we want to crash the process if the _channel is null.
        ApplicationStateGuard.Ensure(_channel is not null);

        try
        {
            // We don't need cancellation token because the task will be stopped when the Channel is completed thanks to the call to Complete() inside the Dispose method.
            while (await _channel.Reader.WaitToReadAsync())
            {
                await _writer.WriteLineAsync(await _channel.Reader.ReadAsync());
            }
        }
        catch (Exception ex)
        {
            _console.WriteLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnexpectedExceptionInFileLoggerErrorMessage, ex));
        }
#else
        // We do this check out of the try because we want to crash the process if the _asyncLogs is null.
        ApplicationStateGuard.Ensure(_asyncLogs is not null);

        try
        {
            // We don't need cancellation token because the task will be stopped when the BlockingCollection is completed thanks to the call to CompleteAdding()
            // inside the Dispose method.
            foreach (string message in _asyncLogs.GetConsumingEnumerable())
            {
                await _writer.WriteLineAsync(message);
            }
        }
        catch (Exception ex)
        {
            _console.WriteLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnexpectedExceptionInFileLoggerErrorMessage, ex));
        }
#endif
    }

#if NETCOREAPP
    [MemberNotNull(nameof(_channel), nameof(_logLoop))]
    private void EnsureAsyncLogObjectsAreNotNull()
    {
        ApplicationStateGuard.Ensure(_channel is not null);
        ApplicationStateGuard.Ensure(_logLoop is not null);
    }
#else
    [MemberNotNull(nameof(_asyncLogs), nameof(_logLoop))]
    private void EnsureAsyncLogObjectsAreNotNull()
    {
        ApplicationStateGuard.Ensure(_asyncLogs is not null);
        ApplicationStateGuard.Ensure(_logLoop is not null);
    }
#endif

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_syncFlush)
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

        if (!_syncFlush)
        {
            EnsureAsyncLogObjectsAreNotNull();

            // Wait for all logs to be written
            _channel.Writer.TryComplete();
            await _logLoop.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        }

        _semaphore.Dispose();
        await _writer.FlushAsync();
        await _fileStream.DisposeAsync();
        _disposed = true;
    }
#endif
}
