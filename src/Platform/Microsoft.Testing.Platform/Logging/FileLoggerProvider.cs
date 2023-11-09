// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class FileLoggerProvider : ILoggerProvider, IDisposable
#if NETCOREAPP
#pragma warning disable SA1001 // Commas should be spaced correctly
    , IAsyncDisposable
#pragma warning restore SA1001 // Commas should be spaced correctly
#endif
{
    private readonly IClock _clock;
    private readonly string _logPrefixName;
    private readonly bool _customDirectory;

    public LogLevel LogLevel { get; }

    public FileLogger FileLogger { get; private set; }

    public bool SyncFlush { get; }

    public FileLoggerProvider(string logFolder, IClock clock, LogLevel logLevel, string logPrefixName, bool customDirectory, bool syncFlush)
    {
        _clock = clock;
        _logPrefixName = logPrefixName;
        _customDirectory = customDirectory;
        FileLogger = new FileLogger(logFolder, clock, logLevel, logPrefixName, syncFlush, new SystemTask(), new SystemConsole());

        LogLevel = logLevel;
        SyncFlush = syncFlush;
    }

    public async Task CheckLogFolderAndMoveToTheNewIfNeededAsync(string testResultDirectory)
    {
        // If custom directory is provided for the log file, we don't WANT to move the log file
        // We won't betray the users expectations.
        if (_customDirectory)
        {
            return;
        }

        if (testResultDirectory != Path.GetDirectoryName(FileLogger.FileName))
        {
            if (FileLogger is not null)
            {
                string fileName = Path.GetFileName(FileLogger.FileName);
#if NETCOREAPP
                await FileLogger.DisposeAsync();
#else
                FileLogger.Dispose();
#endif

                // Move the log file to the new directory
                File.Move(FileLogger.FileName, Path.Combine(testResultDirectory, fileName));

                FileLogger = new FileLogger(testResultDirectory, fileName, _clock, LogLevel, _logPrefixName, SyncFlush, new SystemTask(), new SystemConsole());
            }
        }

#if !NETCOREAPP
        await Task.CompletedTask;
#endif
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
