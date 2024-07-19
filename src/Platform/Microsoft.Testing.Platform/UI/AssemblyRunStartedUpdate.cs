// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

internal sealed class AssemblyRunStartedUpdate(string assembly, int tests, string? targetFramework, string? architecture)
{
    public string Assembly { get; } = assembly;

    public int Tests { get; } = tests;

    public string? TargetFramework { get; } = targetFramework;

    public string? Architecture { get; } = architecture;
}
