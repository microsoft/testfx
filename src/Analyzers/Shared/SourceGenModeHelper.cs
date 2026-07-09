// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Shared;

/// <summary>
/// Reads the <c>MSTestSourceGenMode</c> MSBuild property (surfaced to the compiler via
/// <c>&lt;CompilerVisibleProperty&gt;</c>) that selects which MSTest source-generation strategy emits
/// for a compilation:
/// <list type="bullet">
///   <item><description><c>ReflectionFree</c> (the shipped default, set by
///   <c>MSTest.TestAdapter.targets</c>) — the generator that materializes attributes and emits
///   delegate-based constructor / method / property invokers so the adapter runs without runtime
///   reflection.</description></item>
///   <item><description><c>Rooting</c> — the lean generator that registers test types and methods and
///   roots members with <c>[DynamicDependency]</c>, leaving the adapter to read the rest via runtime
///   reflection.</description></item>
/// </list>
/// Exactly one strategy emits per compilation, so the two generators gate their outputs on this value
/// and never both register the same assembly. When the property is unset (for example when the
/// <c>MSTest.TestAdapter</c> targets are not imported to supply the default) this helper falls back to
/// the rooting strategy.
/// </summary>
internal static class SourceGenModeHelper
{
    private const string BuildPropertyKey = "build_property.MSTestSourceGenMode";
    private const string ReflectionFreeValue = "ReflectionFree";

    /// <summary>
    /// Returns <see langword="true"/> when the consuming project uses the reflection-free strategy
    /// (<c>&lt;MSTestSourceGenMode&gt;ReflectionFree&lt;/MSTestSourceGenMode&gt;</c>, which is the
    /// shipped default supplied by <c>MSTest.TestAdapter.targets</c>); otherwise <see langword="false"/>
    /// for the rooting strategy.
    /// </summary>
    public static bool IsReflectionFree(AnalyzerConfigOptions globalOptions)
        => globalOptions.TryGetValue(BuildPropertyKey, out string? value)
            && string.Equals(value, ReflectionFreeValue, StringComparison.OrdinalIgnoreCase);
}
