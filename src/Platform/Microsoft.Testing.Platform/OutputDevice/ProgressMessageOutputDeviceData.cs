// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a transient message displayed in the terminal progress area.
/// </summary>
/// <remarks>
/// Messages are scoped to their <see cref="Extensions.OutputDevice.IOutputDeviceDataProducer"/> and identified
/// by <see cref="Key"/>. Sending another message with the same key replaces the previous text. A
/// <see langword="null"/> <see cref="Message"/> removes the message from an interactive progress area.
/// When interactive progress is unavailable or disabled, each changed value is written as a durable line instead;
/// those lines remain in terminal scrollback and cannot be removed by a later update.
/// </remarks>
public sealed class ProgressMessageOutputDeviceData : IOutputDeviceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressMessageOutputDeviceData"/> class.
    /// </summary>
    /// <param name="key">The producer-local key used to replace or remove the message.</param>
    /// <param name="message">
    /// The message to display, or <see langword="null"/> to remove it from an interactive progress area.
    /// </param>
    public ProgressMessageOutputDeviceData(string key, string? message)
    {
        Key = key;
        Message = message;
    }

    /// <summary>
    /// Gets the producer-local key used to replace or remove the message.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the message to display, or <see langword="null"/> when the message should be removed.
    /// </summary>
    public string? Message { get; }
}
