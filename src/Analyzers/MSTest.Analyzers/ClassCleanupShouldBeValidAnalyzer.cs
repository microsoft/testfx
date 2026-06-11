// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0011: <inheritdoc cref="Resources.ClassCleanupShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ClassCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ClassCleanupShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.ClassCleanupShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ClassCleanupShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ClassCleanupShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
            FixtureMethodAnalyzerHelper.RegisterFixtureMethodSymbolAction(
                context,
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute,
                static (symbolContext, symbols) => AnalyzeSymbol(symbolContext, symbols)));
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        FixtureMethodAnalyzerHelper.FixtureMethodSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        bool isInheritanceModeSet = methodSymbol.IsInheritanceModeSet(symbols.InheritanceBehaviorSymbol, symbols.FixtureAttributeSymbol);
        if (methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol)
            && (!methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: true,
                allowGenericType: isInheritanceModeSet, FixtureParameterMode.OptionalTestContext, symbols.TestContextSymbol,
                symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: true, out bool isFixable)
                || (!isInheritanceModeSet && methodSymbol.ContainingType.IsAbstract)
                || (isInheritanceModeSet && methodSymbol.ContainingType.IsSealed)))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
