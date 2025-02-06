// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

/// <summary>
/// Represents a logging manager that can be used to add logger providers.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ILoggingManager
{
    /// <summary>
    /// Adds a logger provider factory to the logging manager.
    /// </summary>
    /// <param name="loggerProviderFactory">A function taking a <see cref="LogLevel"/> and a <see cref="IServiceProvider"/> to build the <see cref="ILoggerProvider"/>.</param>
    void AddProvider(Func<LogLevel, IServiceProvider, ILoggerProvider> loggerProviderFactory);
}
