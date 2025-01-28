// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface ILoggingManager
{
    void AddProvider(Func<LogLevel, IServiceProvider, ILoggerProvider> loggerProviderFactory);
}
