// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed record FileLoggerInformation(bool SyncronousWrite, FileInfo LogFile, LogLevel LogLevel) : IFileLoggerInformation
{
    public bool SyncronousWrite { get; init; } = SyncronousWrite;

    public FileInfo LogFile { get; init; } = LogFile;

    public LogLevel LogLevel { get; init; } = LogLevel;
}
