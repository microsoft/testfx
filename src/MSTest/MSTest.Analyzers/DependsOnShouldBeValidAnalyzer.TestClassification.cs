// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class DependsOnShouldBeValidAnalyzer
{
    /// <summary>
    /// Whether the assembly opts internal members into discovery, which decides whether an internal
    /// <c>[TestMethod]</c> is a runnable test (<c>TestMethodValidator.IsValidTestMethod</c>).
    /// </summary>
    private static bool DiscoversInternals(Compilation compilation)
    {
        if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDiscoverInternalsAttribute, out INamedTypeSymbol? discoverInternalsAttributeSymbol))
        {
            return false;
        }

        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, discoverInternalsAttributeSymbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Names a type for a diagnostic message. <see cref="ISymbol.Name"/> is empty for an array or pointer,
    /// so those fall back to the display form (<c>int[]</c>).
    /// </summary>
    private static string DescribeType(ITypeSymbol type)
        => string.IsNullOrEmpty(type.Name)
            ? type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            : type.Name;

    /// <summary>
    /// Whether discovery runs tests under <paramref name="type"/>'s own name. Mirrors
    /// <c>TypeValidator.IsValidTestClass</c> on the points that decide whether a <c>[DependsOn]</c> reference
    /// can ever match: the <c>[TestClass]</c> attribute, accessibility, the fact that an abstract test class
    /// is skipped so its tests are enumerated under each concrete derived class, and the fact that a
    /// non-abstract generic test class is rejected outright.
    /// </summary>
    /// <remarks>
    /// A static class is checked separately from an abstract one. Reflection sees a static class as
    /// <c>abstract sealed</c>, so <c>IsValidTestClass</c> rejects it through its <c>IsAbstract</c> test, but
    /// Roslyn models the two independently and reports <c>IsAbstract == false</c> for it.
    /// </remarks>
    private static bool IsRunnableTestClass(INamedTypeSymbol type, AnalysisSymbols symbols)
        => !type.IsAbstract
            && !type.IsStatic
            && !IsGenericOrNestedInGeneric(type)
            && HasValidAccessibility(type, symbols)
            && type.HasCorrectTestContextSignature()
            && type.IsTestClass(symbols.TestClassAttribute);

    /// <summary>
    /// Whether any generic type is involved. Discovery enumerates the assembly's type <em>definitions</em>,
    /// and <c>TypeValidator.IsValidTestClass</c> rejects a non-abstract generic definition - so no test ever
    /// exists under a generic class's name, whether it is written open (<c>typeof(Tests&lt;&gt;)</c>) or
    /// constructed (<c>typeof(Tests&lt;int&gt;)</c>). A type nested in a generic container is itself a generic
    /// definition at run time, so it is covered too.
    /// </summary>
    private static bool IsGenericOrNestedInGeneric(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mirrors <c>TypeValidator.TypeHasValidAccessibility</c>: the type and every type it is nested in must be
    /// public, or internal when the assembly opts in with <c>[DiscoverInternals]</c>. Anything else - a
    /// private, protected or protected-internal nested class - is skipped by discovery.
    /// </summary>
    private static bool HasValidAccessibility(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility == Accessibility.Public)
            {
                continue;
            }

            if (!symbols.DiscoverInternals || current.DeclaredAccessibility != Accessibility.Internal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether discovery turns <paramref name="method"/> into a test. Mirrors the parts of
    /// <c>TestMethodValidator.IsValidTestMethod</c> that are decidable here: the <c>[TestMethod]</c>
    /// attribute, accessibility (public, or internal when the assembly opts in with
    /// <c>[DiscoverInternals]</c>), and the rejection of static and abstract methods.
    /// </summary>
    /// <remarks>
    /// The return-type rule is deliberately not mirrored. MSTEST0003 already reports a test method with an
    /// invalid signature, so the only thing this omission costs is a graph node for a method that is
    /// reported as broken anyway - whereas getting <c>async void</c> and <c>ValueTask&lt;T&gt;</c> subtly
    /// wrong would drop nodes for methods that really do run.
    /// </remarks>
    private static bool IsRunnableTestMethod(IMethodSymbol method, AnalysisSymbols symbols)
        => method is { IsStatic: false, IsAbstract: false }
            && (method.DeclaredAccessibility == Accessibility.Public
                || (symbols.DiscoverInternals && method.DeclaredAccessibility == Accessibility.Internal))
            && method.IsTestMethod(symbols.TestMethodAttribute);

    private static bool IsFromCurrentAssembly(SymbolAnalysisContext context, INamedTypeSymbol type)
        => SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, context.Compilation.Assembly);
}
