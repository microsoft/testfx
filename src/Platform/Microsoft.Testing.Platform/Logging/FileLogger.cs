// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Threading.Channels;
#endif

using Microsoft.Testing.Platform.Helpers;
#if !NETCOREAPP
using Microsoft.Testing.Platform.Messages;
#endif
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
    private readonly SingleConsumerUnboundedChannel<string>? _channel;
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

        if (_options.SyncFlush)
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException(PlatformResources.SyncFlushNotSupportedInBrowserErrorMessage);
            }
        }
        else
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
            _channel = new SingleConsumerUnboundedChannel<string>();
#endif

            _logLoop = task.Run(WriteLogToFileAsync, CancellationToken.None);
        }

        if (_options.FileName is not null)
        {
            string fileNameFullPath = Path.Combine(_options.LogFolder, _options.FileName);
            _fileStream = fileSystem.ExistFile(fileNameFullPath)
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
                fileName = $"{_options.LogPrefixName}_{_clock.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture)}.diag";
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
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException(PlatformResources.SyncFlushNotSupportedInBrowserErrorMessage);
            }

            InternalSyncLog(logLevel, state, exception, formatter, category);
        }
        else
        {
            EnqueueLog(logLevel, state, exception, formatter, category);
        }
    }

    [UnsupportedOSPlatform("browser")]
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
            await InternalAsyncLogAsync(logLevel, state, exception, formatter, category).ConfigureAwait(false);
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

        if (!await _semaphore.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, PlatformResources.TimeoutAcquiringSemaphoreErrorMessage, TimeoutHelper.DefaultHangTimeoutSeconds));
        }

        try
        {
            await _writer.WriteLineAsync(BuildLogEntry(logLevel, state, exception, formatter, category)).ConfigureAwait(false);
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
        _channel.Write(log);
#endif
    }

    private string BuildLogEntry<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, string category)
        => $"{_clock.UtcNow:O} {category} {logLevel.ToString().ToUpper(CultureInfo.InvariantCulture)} {formatter(state, exception)}";

    private async Task WriteLogToFileAsync()
    {
        // We do this check out of the try because we want to crash the process if the _channel/_asyncLogs is null.
        ApplicationStateGuard.Ensure(_channel is not null);

        try
        {
            // We don't need cancellation token because the task will be stopped when the Channel is completed thanks to the call to Complete() inside the Dispose method.
#if NETCOREAPP
            while (await _channel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                await _writer.WriteLineAsync(await _channel.Reader.ReadAsync().ConfigureAwait(false)).ConfigureAwait(false);
            }
#else
            while (await _channel.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
            {
                while (_channel.TryRead(out string message))
                {
                    await _writer.WriteLineAsync(message).ConfigureAwait(false);
                }
            }
#endif
        }
        catch (Exception ex)
        {
            _console.WriteLine(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnexpectedExceptionInFileLoggerErrorMessage, ex));
        }
    }

    [MemberNotNull(nameof(_channel), nameof(_logLoop))]
    private void EnsureAsyncLogObjectsAreNotNull()
    {
        ApplicationStateGuard.Ensure(_channel is not null);
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

            // Wait for all logs to be written
#if NETCOREAPP
            _channel.Writer.TryComplete();
#else
            _channel.Complete();
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
            await _logLoop.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        }

        _semaphore.Dispose();
        await _writer.FlushAsync().ConfigureAwait(false);
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _fileStream.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
#endif
}
