// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions;

internal static class TargetFrameworkMonikerHelper
{
    public static string GetTargetFrameworkMoniker()
        => TargetFrameworkParser.GetShortTargetFramework(Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription)
            ?? "unknown";
}
