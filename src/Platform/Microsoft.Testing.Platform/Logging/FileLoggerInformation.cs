// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed record FileLoggerInformation(bool SynchronousWrite, FileInfo LogFile, LogLevel LogLevel) : IFileLoggerInformation
{
    public bool SynchronousWrite { get; init; } = SynchronousWrite;

    public FileInfo LogFile { get; init; } = LogFile;

    public LogLevel LogLevel { get; init; } = LogLevel;
}
