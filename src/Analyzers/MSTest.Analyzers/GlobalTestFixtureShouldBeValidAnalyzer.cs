// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0050: <inheritdoc cref="Resources.GlobalTestFixtureShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class GlobalTestFixtureShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.GlobalTestFixtureShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.GlobalTestFixtureShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.GlobalTestFixtureShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.GlobalTestFixtureShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Usage,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestInitializeAttribute, out INamedTypeSymbol? globalTestInitializeAttributeSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingGlobalTestCleanupAttribute, out INamedTypeSymbol? globalTestCleanupAttributeSymbol))
            {
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);

                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, globalTestInitializeAttributeSymbol, globalTestCleanupAttributeSymbol, taskSymbol, valueTaskSymbol, testContextSymbol, testClassAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol globalTestInitializeAttributeSymbol,
        INamedTypeSymbol globalTestCleanupAttributeSymbol,
        INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol,
        INamedTypeSymbol testContextSymbol,
        INamedTypeSymbol testClassAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if ((methodSymbol.HasAttribute(globalTestInitializeAttributeSymbol) || methodSymbol.HasAttribute(globalTestCleanupAttributeSymbol)) &&
            !methodSymbol.HasValidFixtureMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals: false, shouldBeStatic: true,
                allowGenericType: false, FixtureParameterMode.MustHaveTestContext, testContextSymbol, testClassAttributeSymbol, fixtureAllowInheritedTestClass: false, out _))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name));
        }
    }
}
