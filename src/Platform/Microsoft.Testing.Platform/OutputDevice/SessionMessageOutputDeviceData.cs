// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a durable session message that remains visible in the terminal output.
/// </summary>
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
