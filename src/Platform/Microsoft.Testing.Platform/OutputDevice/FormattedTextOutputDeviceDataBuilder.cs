// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Testing.Platform.OutputDevice;

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Do not use this class. This is present temporarily for internal usages (in Retry extension) and will be removed after fixing the internal usages", error: true)]
internal static class FormattedTextOutputDeviceDataBuilder
{
    public static FormattedTextOutputDeviceData CreateGreenConsoleColorText(string text)
        => new(text) { ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Green } };

    public static FormattedTextOutputDeviceData CreateRedConsoleColorText(string text)
        => new(text) { ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Red } };

    public static FormattedTextOutputDeviceData CreateYellowConsoleColorText(string text)
        => new(text) { ForegroundColor = new SystemConsoleColor() { ConsoleColor = ConsoleColor.Yellow } };
}
