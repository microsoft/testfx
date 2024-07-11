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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out INamedTypeSymbol? classCleanupAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? inheritanceBehaviorSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, classCleanupAttributeSymbol, taskSymbol, valueTaskSymbol, inheritanceBehaviorSymbol, testClassAttributeSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classCleanupAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, INamedTypeSymbol? inheritanceBehaviorSymbol, INamedTypeSymbol testClassAttributeSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (methodSymbol.IsClassInitializeMethod(classCleanupAttributeSymbol)
            && !methodSymbol.HasValidFixtureMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals, shouldBeStatic: true,
                allowGenericType: methodSymbol.IsInheritanceModeSet(inheritanceBehaviorSymbol, classCleanupAttributeSymbol), testContextSymbol: null,
                testClassAttributeSymbol, fixtureAllowInheritedTestClass: true, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
