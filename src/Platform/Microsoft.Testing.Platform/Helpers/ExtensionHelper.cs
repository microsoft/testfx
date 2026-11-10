// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.Helpers;

internal static class ExtensionHelper
{
    public static KeyValuePair<string, object?>[] ToOTelTags(this IExtension extension)
        => [
            new(TestingPlatformSemanticConventions.Attributes.TestExtensionUid, extension.Uid),
            new(TestingPlatformSemanticConventions.Attributes.TestExtensionVersion, extension.Version),
            new(TestingPlatformSemanticConventions.Attributes.TestExtensionDisplayName, extension.DisplayName),
        ];
}
