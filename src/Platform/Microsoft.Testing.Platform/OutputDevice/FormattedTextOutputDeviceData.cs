// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

/// <summary>
/// Represents the data for a formatted text output device.
/// </summary>
public sealed class FormattedTextOutputDeviceData : TextOutputDeviceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormattedTextOutputDeviceData"/> class with the specified text.
    /// </summary>
    /// <param name="text">The text to be displayed.</param>
    public FormattedTextOutputDeviceData(string text)
        : base(text)
    {
    }

    /// <summary>
    /// Gets or inits the foreground color of the text.
    /// </summary>
    public IColor? ForegroundColor { get; init; }

    /// <summary>
    /// Gets or inits the background color of the text.
    /// </summary>
    public IColor? BackgroundColor { get; init; }
}
