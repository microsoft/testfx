// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Logging;

internal record ServerLogMessage(LogLevel Level, string Message) : IData
{
    public string DisplayName { get; } = nameof(ServerLogMessage);

    public string Description { get; } = "This data transports internal logs to the server.";
}
