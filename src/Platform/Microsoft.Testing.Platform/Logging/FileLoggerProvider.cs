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
    IFileStreamFactory fileStreamFactory)
    : IFileLoggerProvider, IDisposable
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
        // If custom directory is provided for the log file, we don't WANT to move the log file
        // We won't betray the users expectations.
        if (_customDirectory
            || testResultDirectory == Path.GetDirectoryName(FileLogger.FileName))
        {
            return;
        }

        string fileName = Path.GetFileName(FileLogger.FileName);
        FileLogger previousLogger = FileLogger;
        string previousFileName = previousLogger.FileName;
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
        FileLogger = new FileLogger(
            new FileLoggerOptions(testResultDirectory, _options.LogPrefixName, fileName, _options.SyncFlush),
            LogLevel,
            _clock,
            _task,
            _console,
            _fileSystem,
            _fileStreamFactory);
    }

    public ILogger CreateLogger(string categoryName)
        => new FileLoggerCategory(FileLogger, categoryName);

    public void Dispose()
        => FileLogger.Dispose();

#if NETCOREAPP
    public async ValueTask DisposeAsync()
        => await FileLogger.DisposeAsync().ConfigureAwait(false);
#endif
}
