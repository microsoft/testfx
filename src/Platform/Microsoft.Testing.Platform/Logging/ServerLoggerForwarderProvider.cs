// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLoggerForwarderProvider(IServiceProvider services, LogLevel logLevel) : ILoggerProvider
{
    private readonly IServiceProvider _services = services;
    private readonly LogLevel _logLevel = logLevel;

    public ILogger CreateLogger(string categoryName) => new ServerLoggerForwarder(_services, _logLevel);
}
