// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLoggerForwarderProvider(
    LogLevel logLevel,
    ITask task,
    IServerTestHost? serverTestHost) : ILoggerProvider
{
    private readonly LogLevel _logLevel = logLevel;

    public ILogger CreateLogger(string categoryName) => new ServerLoggerForwarder(_logLevel, task, serverTestHost);
}
