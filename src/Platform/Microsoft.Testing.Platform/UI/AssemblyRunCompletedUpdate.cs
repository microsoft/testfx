// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UI;

internal sealed class AssemblyRunCompletedUpdate(string assembly, string? targetFramework, string? architecture)
{
    public string Assembly { get; } = assembly;

    public string? TargetFramework { get; } = targetFramework;

    public string? Architecture { get; } = architecture;
}
