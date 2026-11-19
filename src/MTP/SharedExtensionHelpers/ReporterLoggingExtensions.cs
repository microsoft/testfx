// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Shared logging helpers for the pipeline reporter extensions (Azure DevOps and GitHub Actions).
/// </summary>
internal static class ReporterLoggingExtensions
{
    /// <summary>
    /// Logs an otherwise-swallowed exception from a reporter lifecycle callback as a warning, so a failure in a
    /// reporter degrades gracefully instead of propagating into the platform's dispatch.
    /// </summary>
    /// <param name="logger">The logger to write the warning to.</param>
    /// <param name="callbackName">The name of the callback where the exception was caught.</param>
    /// <param name="ex">The unexpected exception.</param>
    public static void LogUnexpectedException(this ILogger logger, string callbackName, Exception ex)
    {
        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning($"Unexpected exception in {callbackName}: {ex}");
        }
    }
}
