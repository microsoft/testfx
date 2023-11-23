// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

#if INTERNALIZE_LOGGING
internal interface ILogger
#else
public interface ILogger
#endif
{
    Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter);

    bool IsEnabled(LogLevel logLevel);
}

#if INTERNALIZE_LOGGING
internal interface ILogger<out TCategoryName> : ILogger
#else
public interface ILogger<out TCategoryName> : ILogger
#endif
{
}
