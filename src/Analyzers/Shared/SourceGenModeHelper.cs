// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Shared;

/// <summary>
/// Reads the <c>MSTestSourceGenMode</c> MSBuild property (surfaced to the compiler via
/// <c>&lt;CompilerVisibleProperty&gt;</c>) that selects which MSTest source-generation strategy emits
/// for a compilation:
/// <list type="bullet">
///   <item><description><c>Rooting</c> (default) — the lean generator that registers test types and
///   methods and roots members with <c>[DynamicDependency]</c>, leaving the adapter to read the rest
///   via runtime reflection.</description></item>
///   <item><description><c>ReflectionFree</c> — the generator that additionally materializes
///   attributes and emits delegate-based constructor / method / property invokers so the adapter runs
///   without runtime reflection.</description></item>
/// </list>
/// Exactly one strategy emits per compilation, so the two generators gate their outputs on this value
/// and never both register the same assembly.
/// </summary>
internal static class SourceGenModeHelper
{
    private const string BuildPropertyKey = "build_property.MSTestSourceGenMode";
    private const string ReflectionFreeValue = "ReflectionFree";

    /// <summary>
    /// Returns <see langword="true"/> when the consuming project selected the reflection-free
    /// strategy (<c>&lt;MSTestSourceGenMode&gt;ReflectionFree&lt;/MSTestSourceGenMode&gt;</c>);
    /// otherwise <see langword="false"/> for the default rooting strategy.
    /// </summary>
    public static bool IsReflectionFree(AnalyzerConfigOptions globalOptions)
        => globalOptions.TryGetValue(BuildPropertyKey, out string? value)
            && string.Equals(value, ReflectionFreeValue, StringComparison.OrdinalIgnoreCase);
}
