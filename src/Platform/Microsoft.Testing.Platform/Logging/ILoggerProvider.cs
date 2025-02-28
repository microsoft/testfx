// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// Represents a logger provider that can be used to create loggers.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ILoggerProvider
{
    /// <summary>
    /// Creates a logger with the specified category name.
    /// </summary>
    /// <param name="categoryName">The name of the category.</param>
    /// <returns>A logger.</returns>
    ILogger CreateLogger(string categoryName);
}
