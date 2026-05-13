// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Helpers;

[System.Obsolete("Use PlatformVersion.Version instead. AppVersion will be removed in v3.")]
internal static class AppVersion
{
    public static readonly string DefaultSemVer = PlatformVersion.Version;
}
