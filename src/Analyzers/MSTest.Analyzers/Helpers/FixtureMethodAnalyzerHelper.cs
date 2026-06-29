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

    internal static void RegisterFixtureMethodSymbolAction<TState>(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodSymbols, TState> analyzeSymbolAction,
        TState state,
        bool requireTestContextSymbol = false)
        => context.RegisterCompilationStartAction(context =>
            RegisterFixtureMethodSymbolAction(
                context,
                fixtureAttributeMetadataName,
                analyzeSymbolAction,
                state,
                requireTestContextSymbol));

    internal static void RegisterFixtureMethodSymbolAction<TState>(
        CompilationStartAnalysisContext context,
        string fixtureAttributeMetadataName,
        Action<SymbolAnalysisContext, FixtureMethodSymbols, TState> analyzeSymbolAction,
        TState state,
        bool requireTestContextSymbol = false)
    {
        if (!TryGetFixtureMethodSymbols(context.Compilation, fixtureAttributeMetadataName, out FixtureMethodSymbols symbols)
            || (requireTestContextSymbol && symbols.TestContextSymbol is null))
        {
            return;
        }

        context.RegisterSymbolAction(
            symbolContext => analyzeSymbolAction(symbolContext, symbols, state),
            SymbolKind.Method);
    }

    internal static void RegisterInstanceFixtureAnalyzer(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        DiagnosticDescriptor rule)
        => RegisterFixtureMethodSymbolAction(
            context,
            fixtureAttributeMetadataName,
            static (symbolContext, symbols, rule) => AnalyzeInstanceFixtureMethod(symbolContext, symbols, rule),
            rule);

    internal static void RegisterClassFixtureAnalyzer(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        DiagnosticDescriptor rule,
        FixtureParameterMode parameterMode)
        => RegisterFixtureMethodSymbolAction(
            context,
            fixtureAttributeMetadataName,
            static (symbolContext, symbols, state) => AnalyzeClassFixtureMethod(symbolContext, symbols, state.rule, state.parameterMode),
            (rule, parameterMode),
            requireTestContextSymbol: parameterMode == FixtureParameterMode.MustHaveTestContext);

    internal static void RegisterAssemblyFixtureAnalyzer(
        AnalysisContext context,
        string fixtureAttributeMetadataName,
        DiagnosticDescriptor rule,
        FixtureParameterMode parameterMode)
        => RegisterFixtureMethodSymbolAction(
            context,
            fixtureAttributeMetadataName,
            static (symbolContext, symbols, state) => AnalyzeAssemblyFixtureMethod(symbolContext, symbols, state.rule, state.parameterMode),
            (rule, parameterMode),
            requireTestContextSymbol: parameterMode == FixtureParameterMode.MustHaveTestContext);

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

    internal static void AnalyzeClassFixtureMethod(
        SymbolAnalysisContext context,
        FixtureMethodSymbols symbols,
        DiagnosticDescriptor rule,
        FixtureParameterMode parameterMode)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (!methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol))
        {
            return;
        }

        bool isInheritanceModeSet = methodSymbol.IsInheritanceModeSet(symbols.InheritanceBehaviorSymbol, symbols.FixtureAttributeSymbol);
        if ((!methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: true,
                allowGenericType: isInheritanceModeSet, parameterMode, symbols.TestContextSymbol,
                symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: true, out bool isFixable))
            || (!isInheritanceModeSet && methodSymbol.ContainingType.IsAbstract)
            || (isInheritanceModeSet && methodSymbol.ContainingType.IsSealed))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }

    internal static void AnalyzeAssemblyFixtureMethod(
        SymbolAnalysisContext context,
        FixtureMethodSymbols symbols,
        DiagnosticDescriptor rule,
        FixtureParameterMode parameterMode)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol)
            && !methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: true,
                allowGenericType: false, parameterMode, symbols.TestContextSymbol,
                symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: false, out bool isFixable))
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
