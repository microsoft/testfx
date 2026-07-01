// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions;

internal static class TargetFrameworkMonikerHelper
{
    public static string GetTargetFrameworkMonikerIncludingPlatform()
        => TargetFrameworkParser.GetShortTargetFrameworkIncludingPlatform(Assembly.GetEntryAssembly())
            ?? "unknown framework";

    public static string GetTargetFrameworkMonikerWithRuntimeFallback()
        => TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
            ?? "unknown framework";
}
