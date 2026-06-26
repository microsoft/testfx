// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodAnalyzerHelper
{
    internal static LocalizableResourceString CreateResourceString(string resourceName)
        => new(resourceName, Resources.ResourceManager, typeof(Resources));

    internal static bool TryGetFixtureMethodSymbols(
        Compilation compilation,
        string fixtureAttributeMetadataName,
        out FixtureMethodSymbols symbols)
    {
        if (!compilation.TryGetOrCreateTypeByMetadataName(fixtureAttributeMetadataName, out INamedTypeSymbol? fixtureAttributeSymbol)
            || !compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
        {
            symbols = default;
            return false;
        }

        symbols = new FixtureMethodSymbols(
            fixtureAttributeSymbol,
            testClassAttributeSymbol,
            compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask),
            compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask),
            compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext),
            compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior),
            compilation.CanDiscoverInternals());

        return true;
    }

    internal static void RegisterFixtureMethodSymbolAction(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodSymbols> analyzeSymbolAction,
        bool requireTestContextSymbol = false)
        => context.RegisterCompilationStartAction(context =>
            RegisterFixtureMethodSymbolAction(
                context,
                fixtureAttributeMetadataName,
                analyzeSymbolAction,
                requireTestContextSymbol));

    internal static void RegisterFixtureMethodSymbolAction(
        CompilationStartAnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodSymbols> analyzeSymbolAction,
        bool requireTestContextSymbol = false)
    {
        if (!TryGetFixtureMethodSymbols(context.Compilation, fixtureAttributeMetadataName, out FixtureMethodSymbols symbols)
            || (requireTestContextSymbol && symbols.TestContextSymbol is null))
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => analyzeSymbolAction(symbolContext, symbols),
            SymbolKind.Method);
    }

    internal static void AnalyzeInstanceFixtureMethod(
        SymbolAnalysisContext context,
        FixtureMethodSymbols symbols,
        DiagnosticDescriptor rule)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol)
            && !methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: false,
                allowGenericType: true, FixtureParameterMode.MustNotHaveTestContext, testContextSymbol: null,
                symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: true, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }

    internal readonly record struct FixtureMethodSymbols(
        INamedTypeSymbol FixtureAttributeSymbol,
        INamedTypeSymbol TestClassAttributeSymbol,
        INamedTypeSymbol? TaskSymbol,
        INamedTypeSymbol? ValueTaskSymbol,
        INamedTypeSymbol? TestContextSymbol,
        INamedTypeSymbol? InheritanceBehaviorSymbol,
        bool CanDiscoverInternals);
}
