// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodAnalyzerHelper
{
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
            compilation.CanDiscoverInternals());

        return true;
    }

    internal static void RegisterFixtureMethodSymbolAction(
        CompilationStartAnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodSymbols> analyzeSymbolAction)
    {
        if (!TryGetFixtureMethodSymbols(context.Compilation, fixtureAttributeMetadataName, out FixtureMethodSymbols symbols))
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => analyzeSymbolAction(symbolContext, symbols),
            SymbolKind.Method);
    }

    internal readonly record struct FixtureMethodSymbols(
        INamedTypeSymbol FixtureAttributeSymbol,
        INamedTypeSymbol TestClassAttributeSymbol,
        INamedTypeSymbol? TaskSymbol,
        INamedTypeSymbol? ValueTaskSymbol,
        bool CanDiscoverInternals);
}
