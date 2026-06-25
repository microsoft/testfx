// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0013: <inheritdoc cref="Resources.AssemblyCleanupShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyCleanupShouldBeValidRuleId,
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidTitle)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat)),
        FixtureMethodAnalyzerHelper.CreateResourceString(nameof(Resources.AssemblyCleanupShouldBeValidDescription)),
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
        FixtureMethodAnalyzerHelper.RegisterFixtureMethodSymbolAction(
            context,
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute,
            AnalyzeSymbol);
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        FixtureMethodAnalyzerHelper.FixtureMethodSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol))
        {
            return;
        }

        if (!methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: true, allowGenericType: false, FixtureParameterMode.OptionalTestContext, symbols.TestContextSymbol, symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: false, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
