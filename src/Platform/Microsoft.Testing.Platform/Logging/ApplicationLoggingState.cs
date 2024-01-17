// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ApplicationLoggingState
{
    public ApplicationLoggingState(LogLevel logLevel, CommandLineParseResult commandLineParseResult)
    {
        LogLevel = logLevel;
        CommandLineParseResult = commandLineParseResult;
    }

    public ApplicationLoggingState(LogLevel logLevel, CommandLineParseResult commandLineParseResult, FileLoggerProvider fileLoggerProvider, bool isSynchronousWrite)
        : this(logLevel, commandLineParseResult)
    {
        FileLoggerProvider = fileLoggerProvider;
        IsSynchronousWrite = isSynchronousWrite;
    }

    public FileLoggerProvider? FileLoggerProvider { get; }

    public LogLevel LogLevel { get; }

    public CommandLineParseResult CommandLineParseResult { get; }

    public bool IsSynchronousWrite { get; }
}
