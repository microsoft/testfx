// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.Testing.Platform.OutputDevice;

[EditorBrowsable(EditorBrowsableState.Never)]
[Obsolete("Do not use this class. This is present temporarily for internal usages and will be removed after fixing the internal usages", error: true)]
internal static class FormattedTextOutputDeviceDataBuilder
{
    public static IOutputDeviceData CreateGreenConsoleColorText(string text)
           => new TextOutputDeviceData(text);

    public static IOutputDeviceData CreateRedConsoleColorText(string text)
           => new ErrorMessageOutputDeviceData(text);

    public static IOutputDeviceData CreateYellowConsoleColorText(string text)
        => new WarningMessageOutputDeviceData(text);
}
