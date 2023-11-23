// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ConsoleLoggerProvider(LogLevel logLevel, IConsole console, IClock clock) : ILoggerProvider
{
    private readonly LogLevel _logLevel = logLevel;
    private readonly IConsole _console = console;
    private readonly IClock _clock = clock;

    public ILogger CreateLogger(string categoryName)
        => new ConsoleLogger(_logLevel, _console, _clock, categoryName);
}
