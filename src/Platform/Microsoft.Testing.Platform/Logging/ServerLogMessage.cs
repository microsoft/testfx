// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Resources;

namespace Microsoft.Testing.Platform.Logging;

internal sealed class ServerLogMessage(LogLevel level, string message) : IData
{
    public string DisplayName { get; } = PlatformResources.ServerLogMessageDisplayName;

    public string Description { get; } = PlatformResources.ServerLogMessageDescription;

    public LogLevel Level { get; } = level;

    public string Message { get; } = message;
}
