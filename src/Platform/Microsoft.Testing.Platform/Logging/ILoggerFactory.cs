// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// Represents a factory for creating loggers.
/// </summary>
public interface ILoggerFactory
{
    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    /// <param name="categoryName">The name of the category for the logger.</param>
    /// <returns>A new instance of the logger.</returns>
    ILogger CreateLogger(string categoryName);
}
