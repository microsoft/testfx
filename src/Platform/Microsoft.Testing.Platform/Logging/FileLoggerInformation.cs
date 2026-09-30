// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed record FileLoggerInformation(bool SynchronousWrite, FileInfo LogFile, LogLevel LogLevel)
    : IFileLoggerInformation
{
    // Retained because this tracked internal API has shipped. The service provider now registers the
    // live DiagnosticLoggingInformation implementation so consumers observe logger relocation.
    public bool SynchronousWrite { get; init; } = SynchronousWrite;

    public FileInfo LogFile { get; init; } = LogFile;

    public LogLevel LogLevel { get; init; } = LogLevel;
}

internal sealed class DiagnosticLoggingInformation(FileLoggerProvider fileLoggerProvider)
    : IFileLoggerInformation, IDiagnosticLoggingInformation
{
    public bool SynchronousWrite => fileLoggerProvider.SyncFlush;

    public FileInfo LogFile => new(fileLoggerProvider.FileLogger.FileName);

    public LogLevel LogLevel => fileLoggerProvider.LogLevel;
}
