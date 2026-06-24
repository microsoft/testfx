// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureMethodDiagnosticAnalyzer
{
    internal static LocalizableResourceString CreateResourceString(string resourceName)
        => new(resourceName, Resources.ResourceManager, typeof(Resources));

    internal static void RegisterFixtureMethodSymbolAction(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodAnalyzerHelper.FixtureMethodSymbols> analyzeSymbolAction,
        bool requireTestContextSymbol = false)
        => context.RegisterCompilationStartAction(context =>
            FixtureMethodAnalyzerHelper.RegisterFixtureMethodSymbolAction(
                context,
                fixtureAttributeMetadataName,
                analyzeSymbolAction,
                requireTestContextSymbol));
}
