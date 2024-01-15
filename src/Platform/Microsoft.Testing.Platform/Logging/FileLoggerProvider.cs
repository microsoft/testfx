// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerProvider(
    FileLoggerOptions options,
    LogLevel logLevel,
    bool customDirectory,
    IClock clock,
    ITask task,
    IConsole console,
    IFileSystem fileSystem,
    IFileStreamFactory fileStreamFactory,
    IStreamWriterFactory streamWriterFactory) : IFileLoggerProvider, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly FileLoggerOptions _options = options;
    private readonly IClock _clock = clock;
    private readonly ITask _task = task;
    private readonly IConsole _console = console;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly bool _customDirectory = customDirectory;
    private readonly IFileStreamFactory _fileStreamFactory = fileStreamFactory;
    private readonly IStreamWriterFactory _streamWriterFactory = streamWriterFactory;

    public LogLevel LogLevel { get; } = logLevel;

    public FileLogger FileLogger { get; private set; } = new(
        options,
        logLevel,
        clock,
        task,
        console,
        fileSystem,
        fileStreamFactory,
        streamWriterFactory);

    public bool SyncFlush => _options.SyncFlush;

    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync(string testResultDirectory)
    {
        // If custom directory is provided for the log file, we don't WANT to move the log file
        // We won't betray the users expectations.
        if (_customDirectory
            || testResultDirectory == Path.GetDirectoryName(FileLogger.FileName)
            || FileLogger is null)
        {
            return;
        }

        string fileName = Path.GetFileName(FileLogger.FileName);
        await DisposeHelper.DisposeAsync(FileLogger);

        // Move the log file to the new directory
        _fileSystem.Move(FileLogger.FileName, Path.Combine(testResultDirectory, fileName));

        FileLogger = new FileLogger(
            new FileLoggerOptions(testResultDirectory, _options.LogPrefixName, fileName, _options.SyncFlush),
            LogLevel,
            _clock,
            _task,
            _console,
            _fileSystem,
            _fileStreamFactory,
            _streamWriterFactory);
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLoggerCategory(FileLogger, categoryName);

    public void Dispose()
        => FileLogger?.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (FileLogger is not null)
        {
            await FileLogger.DisposeAsync();
        }
    }
#endif
}
