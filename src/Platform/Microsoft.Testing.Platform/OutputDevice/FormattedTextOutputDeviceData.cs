// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

public record FormattedTextOutputDeviceData(string Text) : TextOutputDeviceData(Text)
{
    public IColor? ForegroundColor { get; init; }

    public IColor? BackgroundColor { get; init; }
}
