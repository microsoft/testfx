// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// A set of extension methods for <see cref="ILogger"/>.
/// </summary>
public static class LoggingExtensions
{
    internal static readonly Func<string, Exception?, string> Formatter =
        (state, exception) =>
            exception is not null
#pragma warning disable RS0030 // Do not use banned APIs
                ? $"{state}{Environment.NewLine}------Exception detail------{Environment.NewLine}{exception}"
#pragma warning restore RS0030 // Do not use banned APIs
                : state;

    /// <summary>
    /// Logs a message with the trace log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    public static Task LogTraceAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Trace, message, null, Formatter);

    /// <summary>
    /// Logs a message with the debug log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    public static Task LogDebugAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Debug, message, null, Formatter);

    /// <summary>
    /// Logs a message with the info log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    public static Task LogInformationAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Information, message, null, Formatter);

    /// <summary>
    /// Logs a message with the warning log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    public static Task LogWarningAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Warning, message, null, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    public static Task LogErrorAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Error, message, null, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    /// <param name="ex">The exception.</param>
    public static Task LogErrorAsync(this ILogger logger, string message, Exception ex)
        => logger.LogAsync(LogLevel.Error, message, ex, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="ex">The exception.</param>
    public static Task LogErrorAsync(this ILogger logger, Exception ex)
        => logger.LogAsync(LogLevel.Error, ex.ToString(), null, Formatter);

    /// <summary>
    /// Logs a message with the critical log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static Task LogCriticalAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Critical, message, null, Formatter);

    /// <summary>
    /// Logs a message with the trace log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogTrace(this ILogger logger, string message)
        => logger.Log(LogLevel.Trace, message, null, Formatter);

    /// <summary>
    /// Logs a message with the debug log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogDebug(this ILogger logger, string message)
        => logger.Log(LogLevel.Debug, message, null, Formatter);

    /// <summary>
    /// Logs a message with the info log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogInformation(this ILogger logger, string message)
        => logger.Log(LogLevel.Information, message, null, Formatter);

    /// <summary>
    /// Logs a message with the warning log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogWarning(this ILogger logger, string message)
        => logger.Log(LogLevel.Warning, message, null, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogError(this ILogger logger, string message)
        => logger.Log(LogLevel.Error, message, null, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    /// <param name="ex">The exception.</param>
    public static void LogError(this ILogger logger, string message, Exception ex)
        => logger.Log(LogLevel.Error, message, ex, Formatter);

    /// <summary>
    /// Logs a message with the error log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="ex">The exception.</param>
    public static void LogError(this ILogger logger, Exception ex)
        => logger.Log(LogLevel.Error, ex.ToString(), null, Formatter);

    /// <summary>
    /// Logs a message with the critical log level.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message to log.</param>
    public static void LogCritical(this ILogger logger, string message)
        => logger.Log(LogLevel.Critical, message, null, Formatter);
}
