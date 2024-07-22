// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice;

internal static class FormattedTextOutputDeviceDataBuilder
{
    public static FormattedTextOutputDeviceData CreateGreenConsoleColorText(string text)
           => new(text) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Green } };

    public static FormattedTextOutputDeviceData CreateRedConsoleColorText(string text)
           => new(text) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Red } };

    public static FormattedTextOutputDeviceData CreateYellowConsoleColorText(string text)
        => new(text) { ForegroundColor = new SystemConsoleColor { ConsoleColor = ConsoleColor.Yellow } };
}
