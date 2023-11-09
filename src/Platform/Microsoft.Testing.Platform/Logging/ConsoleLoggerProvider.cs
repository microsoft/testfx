// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ConsoleLoggerProvider : ILoggerProvider
{
    private readonly LogLevel _logLevel;
    private readonly IConsole _console;
    private readonly IClock _clock;

    public ConsoleLoggerProvider(LogLevel logLevel, IConsole console, IClock clock)
    {
        _logLevel = logLevel;
        _console = console;
        _clock = clock;
    }

    public ILogger CreateLogger(string categoryName)
        => new ConsoleLogger(_logLevel, _console, _clock, categoryName);
}
