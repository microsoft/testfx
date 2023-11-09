// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ConsoleLogger : ILogger
{
    private readonly LogLevel _logLevel;
    private readonly IConsole _console;
    private readonly IClock _clock;
    private readonly string _category;

    public ConsoleLogger(LogLevel logLevel, IConsole console, IClock clock, string category)
    {
        _logLevel = logLevel;
        _console = console;
        _clock = clock;
        _category = category;
    }

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _console.WriteLine($"[{_clock.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {_category} - {logLevel}] {formatter(state, exception)}");

    public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _console.WriteLine($"[{_clock.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {_category} - {logLevel}] {formatter(state, exception)}");
        return Task.CompletedTask;
    }
}
