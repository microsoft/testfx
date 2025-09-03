// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ExtensionHelper
{
    public static IEnumerable<KeyValuePair<string, object?>> ToOTelTags(this IExtension extension)
        => new KeyValuePair<string, object?>[]
        {
            new("Extension.UID", extension.Uid),
            new("Extension.Version", extension.Version),
            new("Extension.DisplayName", extension.DisplayName),
            new("Extension.Description", extension.Description),
        };
}
