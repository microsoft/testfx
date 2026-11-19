// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a durable session message that remains visible in the terminal output.
/// </summary>
/// <remarks>
/// Under the <c>dotnet test</c> pipe protocol, delivery requires an active connection that negotiates protocol
/// version 1.3.0 or later. The host drops the message when no connection is available or an older protocol is
/// negotiated because those SDK versions cannot deserialize informational display messages.
/// </remarks>
public sealed class SessionMessageOutputDeviceData : IOutputDeviceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionMessageOutputDeviceData"/> class.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public SessionMessageOutputDeviceData(string message) => Message = message;

    /// <summary>
    /// Gets the message to display.
    /// </summary>
    public string Message { get; }
}
