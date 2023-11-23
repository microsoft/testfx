// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerProvider(string logFolder, LogLevel logLevel, string logPrefixName, bool customDirectory, bool syncFlush,
    IClock clock, ITask task, IConsole console) : ILoggerProvider, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IClock _clock = clock;
    private readonly ITask _task = task;
    private readonly IConsole _console = console;
    private readonly string _logPrefixName = logPrefixName;
    private readonly bool _customDirectory = customDirectory;

    public LogLevel LogLevel { get; } = logLevel;

    public FileLogger FileLogger { get; private set; } = new FileLogger(logFolder, fileName: null, logLevel, logPrefixName, syncFlush, clock, task, console);

    public bool SyncFlush { get; } = syncFlush;

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
        File.Move(FileLogger.FileName, Path.Combine(testResultDirectory, fileName));

        FileLogger = new FileLogger(testResultDirectory, fileName, LogLevel, _logPrefixName, SyncFlush, _clock, _task, _console);
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
