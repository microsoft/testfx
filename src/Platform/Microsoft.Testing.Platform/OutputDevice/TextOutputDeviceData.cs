// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents a text data for directed to the output device.
/// </summary>
public class TextOutputDeviceData : IOutputDeviceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextOutputDeviceData"/> class with the specified text.
    /// </summary>
    /// <param name="text">The text for the output device.</param>
    public TextOutputDeviceData(string text) => Text = text;

    /// <summary>
    /// Gets the text for the output device.
    /// </summary>
    public string Text { get; }
}
