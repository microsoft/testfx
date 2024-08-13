// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// Provides extension methods for the <see cref="ILoggerFactory"/> interface.
/// </summary>
public static class LoggerFactoryExtensions
{
    /// <summary>
    /// Creates a logger instance for the specified category name.
    /// </summary>
    /// <typeparam name="TCategoryName">The type of the category name.</typeparam>
    /// <param name="factory">The logger factory.</param>
    /// <returns>A logger instance.</returns>
    public static ILogger<TCategoryName> CreateLogger<TCategoryName>(this ILoggerFactory factory)
    {
        Guard.NotNull(factory);
        return new Logger<TCategoryName>(factory);
    }
}
