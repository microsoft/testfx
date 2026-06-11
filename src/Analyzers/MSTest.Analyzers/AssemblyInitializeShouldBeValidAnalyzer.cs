// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0012: <inheritdoc cref="Resources.AssemblyInitializeShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyInitializeShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.AssemblyInitializeShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.AssemblyInitializeShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.AssemblyInitializeShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
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
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute,
                static (symbolContext, symbols) => AnalyzeSymbol(symbolContext, symbols),
                requireTestContextSymbol: true));
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, FixtureMethodAnalyzerHelper.FixtureMethodSymbols symbols)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol.HasAttribute(symbols.FixtureAttributeSymbol)
            && !methodSymbol.HasValidFixtureMethodSignature(symbols.TaskSymbol, symbols.ValueTaskSymbol, symbols.CanDiscoverInternals, shouldBeStatic: true,
                allowGenericType: false, FixtureParameterMode.MustHaveTestContext, symbols.TestContextSymbol, symbols.TestClassAttributeSymbol, fixtureAllowInheritedTestClass: false, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
