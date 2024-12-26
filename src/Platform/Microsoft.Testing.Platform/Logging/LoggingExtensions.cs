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
    /// Asynchronously logs a message with <see cref="LogLevel.Trace"/>.
    /// </summary>
    public static Task LogTraceAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Trace, message, null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Debug"/>.
    /// </summary>
    public static Task LogDebugAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Debug, message, null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Information"/>.
    /// </summary>
    public static Task LogInformationAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Information, message, null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Warning"/>.
    /// </summary>
    public static Task LogWarningAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Warning, message, null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Error"/>.
    /// </summary>
    public static Task LogErrorAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Error, message, null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Error"/> which is associated with an exception.
    /// </summary>
    public static Task LogErrorAsync(this ILogger logger, string message, Exception ex)
        => logger.LogAsync(LogLevel.Error, message, ex, Formatter);

    /// <summary>
    /// Asynchronously logs an exception with <see cref="LogLevel.Error"/>.
    /// </summary>
    public static Task LogErrorAsync(this ILogger logger, Exception ex)
    => logger.LogAsync(LogLevel.Error, ex.ToString(), null, Formatter);

    /// <summary>
    /// Asynchronously logs a message with <see cref="LogLevel.Critical"/>.
    /// </summary>
    public static Task LogCriticalAsync(this ILogger logger, string message)
        => logger.LogAsync(LogLevel.Critical, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Trace"/>.
    /// </summary>
    public static void LogTrace(this ILogger logger, string message)
    => logger.Log(LogLevel.Trace, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Debug"/>.
    /// </summary>
    public static void LogDebug(this ILogger logger, string message)
        => logger.Log(LogLevel.Debug, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Information"/>.
    /// </summary>
    public static void LogInformation(this ILogger logger, string message)
        => logger.Log(LogLevel.Information, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Warning"/>.
    /// </summary>
    public static void LogWarning(this ILogger logger, string message)
        => logger.Log(LogLevel.Warning, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Error"/>.
    /// </summary>
    public static void LogError(this ILogger logger, string message)
        => logger.Log(LogLevel.Error, message, null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Error"/> which is associated with an exception.
    /// </summary>
    public static void LogError(this ILogger logger, string message, Exception ex)
        => logger.Log(LogLevel.Error, message, ex, Formatter);

    /// <summary>
    /// Logs an exception with <see cref="LogLevel.Error"/>.
    /// </summary>
    public static void LogError(this ILogger logger, Exception ex)
        => logger.Log(LogLevel.Error, ex.ToString(), null, Formatter);

    /// <summary>
    /// Logs a message with <see cref="LogLevel.Critical"/>.
    /// </summary>
    public static void LogCritical(this ILogger logger, string message)
        => logger.Log(LogLevel.Critical, message, null, Formatter);
}
