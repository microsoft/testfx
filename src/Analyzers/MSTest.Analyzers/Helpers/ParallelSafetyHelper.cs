// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Helpers;

/// <summary>
/// Shared logic for the parallel-safety analyzers (MSTEST0074-MSTEST0077). These rules only describe a
/// live defect when in-assembly parallelization is actually enabled, and they only apply to code that
/// runs as part of an MSTest test.
/// </summary>
internal static class ParallelSafetyHelper
{
    /// <summary>
    /// The <c>.editorconfig</c> / global analyzer option that forces the parallel-safety rules to fire
    /// even when <c>[assembly: Parallelize]</c> is not syntactically present. Set it to <c>always</c> for
    /// suites that enable parallelization through a runsettings file or the <c>MSTestParallelizeScope</c>
    /// MSBuild property, neither of which the analyzer can observe.
    /// </summary>
    internal const string ConfigOptionKey = "mstest_parallel_safety_mode";

    /// <summary>
    /// Diagnostic property key carrying the <c>WellKnownResources</c> member name that the
    /// <c>AddResourceLockFixer</c> code fix should reference. Absent when no code fix should be offered.
    /// </summary>
    internal const string ResourceMemberPropertyKey = "ResourceMember";

    private const string AlwaysValue = "always";

    /// <summary>
    /// Determines whether the parallel-safety rules should produce diagnostics for this compilation. An
    /// analyzer cannot read runsettings and does not currently observe the <c>MSTestParallelizeScope</c>
    /// MSBuild property, so we fire when either the <c>mstest_parallel_safety_mode = always</c> opt-in is
    /// set, or <c>[assembly: Parallelize]</c> is present in source.
    /// </summary>
    internal static bool IsParallelizationInEffect(Compilation compilation, AnalyzerOptions options, INamedTypeSymbol? parallelizeAttributeSymbol)
    {
        if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(ConfigOptionKey, out string? mode)
            && string.Equals(mode, AlwaysValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (parallelizeAttributeSymbol is null)
        {
            return false;
        }

        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parallelizeAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Collects the attribute symbols that mark a method as MSTest "test code" - test methods run through
    /// the parallel scheduler as well as the per-test/per-class/per-assembly fixture methods that run in
    /// the same scheduling chunk.
    /// </summary>
    internal static ImmutableHashSet<INamedTypeSymbol> GetFixtureAttributeSymbols(Compilation compilation)
    {
        ImmutableHashSet<INamedTypeSymbol>.Builder builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute);
        AddIfPresent(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute);
        return builder.ToImmutable();

        void AddIfPresent(string metadataName)
        {
            if (compilation.GetOrCreateTypeByMetadataName(metadataName) is { } symbol)
            {
                builder.Add(symbol);
            }
        }
    }

    /// <summary>
    /// Walks up through local functions / lambdas to the enclosing user-declared method and returns it when
    /// that method is an MSTest test method (including inheritors of <c>[TestMethod]</c>) or one of the
    /// fixture methods in <paramref name="fixtureAttributeSymbols"/>. Returns <see langword="null"/> when the
    /// operation is not inside test code, so callers stay silent on production code and on non-MSTest
    /// frameworks (for example the internal <c>TestContainer</c> framework, whose methods carry no MSTest
    /// attributes).
    /// </summary>
    internal static IMethodSymbol? GetEnclosingTestMethod(
        ISymbol? containingSymbol,
        ImmutableHashSet<INamedTypeSymbol> fixtureAttributeSymbols,
        INamedTypeSymbol? testMethodAttributeSymbol)
    {
        ISymbol? current = containingSymbol;
        while (current is IMethodSymbol method)
        {
            foreach (AttributeData attribute in method.GetAttributes())
            {
                INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                if (attributeClass is null)
                {
                    continue;
                }

                if (fixtureAttributeSymbols.Contains(attributeClass))
                {
                    return method;
                }

                if (testMethodAttributeSymbol is not null && attributeClass.Inherits(testMethodAttributeSymbol))
                {
                    return method;
                }
            }

            // Only keep walking when the current symbol is synthesized from a local function or lambda body;
            // otherwise a nested non-test method should not inherit its container's test-ness.
            if (method.MethodKind is MethodKind.LocalFunction or MethodKind.AnonymousFunction)
            {
                current = method.ContainingSymbol;
                continue;
            }

            return null;
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types opts out of parallelization with <c>[DoNotParallelize]</c>. Such tests run in the
    /// sequential phase, so their process-global mutations cannot race a sibling and must not be flagged.
    /// </summary>
    internal static bool IsOptedOutOfParallelization(IMethodSymbol method, INamedTypeSymbol? doNotParallelizeAttributeSymbol)
    {
        if (doNotParallelizeAttributeSymbol is null)
        {
            return false;
        }

        if (HasAttribute(method, doNotParallelizeAttributeSymbol))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (HasAttribute(type, doNotParallelizeAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types already declares a <c>[ResourceLock]</c> whose resource key equals
    /// <paramref name="resourceKey"/> (ordinal, case-sensitive - matching the runtime's conflict rule). Such
    /// a mutation is coordinated and must not be flagged by the "undeclared mutation" rules.
    /// </summary>
    internal static bool HasResourceLockFor(
        IMethodSymbol method,
        INamedTypeSymbol? resourceLockAttributeSymbol,
        string resourceKey)
    {
        if (resourceLockAttributeSymbol is null)
        {
            return false;
        }

        if (DeclaresResourceLock(method, resourceLockAttributeSymbol, resourceKey))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (DeclaresResourceLock(type, resourceLockAttributeSymbol, resourceKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the test <paramref name="method"/> or any of its (possibly base)
    /// containing types declares any <c>[ResourceLock]</c>. Used by rules whose resource has no well-known key
    /// (for example culture): the presence of any explicit lock is treated as the author having coordinated
    /// access, so the rule stays silent rather than guessing which custom key maps to the resource.
    /// </summary>
    internal static bool HasAnyResourceLock(IMethodSymbol method, INamedTypeSymbol? resourceLockAttributeSymbol)
    {
        if (resourceLockAttributeSymbol is null)
        {
            return false;
        }

        if (HasAttribute(method, resourceLockAttributeSymbol))
        {
            return true;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.BaseType)
        {
            if (HasAttribute(type, resourceLockAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DeclaresResourceLock(ISymbol symbol, INamedTypeSymbol resourceLockAttributeSymbol, string resourceKey)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, resourceLockAttributeSymbol))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length > 0
                && attribute.ConstructorArguments[0].Value is string declaredKey
                && string.Equals(declaredKey, resourceKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol))
            {
                return true;
            }
        }

        return false;
    }
}
