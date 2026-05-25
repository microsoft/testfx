// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;

/// <summary>
/// Tracks whether MSTest's runtime should use the source-generated reflection metadata or fall
/// back to the regular reflection-based code paths. The toggle is one-way: once flipped to
/// <see langword="true"/> it stays <see langword="true"/>. It is safe to call
/// <see cref="Enable"/> from multiple threads or to call it more than once; subsequent calls
/// are no-ops.
/// </summary>
internal static class SourceGeneratorToggle
{
    private static int s_useSourceGenerator;

    public static bool UseSourceGenerator => Volatile.Read(ref s_useSourceGenerator) != 0;

    public static void Enable() => Interlocked.Exchange(ref s_useSourceGenerator, 1);
}
