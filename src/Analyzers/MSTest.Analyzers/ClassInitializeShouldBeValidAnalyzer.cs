// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0010: <inheritdoc cref="Resources.ClassInitializeShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ClassInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ClassInitializeShouldBeValidRuleId,
        new LocalizableResourceString(nameof(Resources.ClassInitializeShouldBeValidTitle), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ClassInitializeShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(nameof(Resources.ClassInitializeShouldBeValidDescription), Resources.ResourceManager, typeof(Resources)),
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
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute, out INamedTypeSymbol? classInitializeAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                || !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior, out INamedTypeSymbol? inheritanceBehavior))
            {
                return;
            }

            INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
            INamedTypeSymbol? inheritanceBehaviorSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior);
            INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
            bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, classInitializeAttributeSymbol, taskSymbol, valueTaskSymbol, testContextSymbol, inheritanceBehaviorSymbol, testClassAttributeSymbol, canDiscoverInternals, inheritanceBehavior),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classInitializeAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, INamedTypeSymbol testContextSymbol, INamedTypeSymbol? inheritanceBehaviorSymbol, INamedTypeSymbol testClassAttributeSymbol,
        bool canDiscoverInternals, INamedTypeSymbol? inheritanceBehavior)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool hasInheritanceBehavior = false;
        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, classInitializeAttributeSymbol)
                && methodAttribute.ConstructorArguments.Any(arg => SymbolEqualityComparer.Default.Equals(arg.Type, inheritanceBehavior)))
            {
                hasInheritanceBehavior = true;
            }
        }

        if (methodSymbol.IsClassInitializeMethod(classInitializeAttributeSymbol)
            && (!methodSymbol.ContainingType.IsAbstract || hasInheritanceBehavior)
            && !methodSymbol.HasValidFixtureMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals, shouldBeStatic: true,
                allowGenericType: methodSymbol.IsInheritanceModeSet(inheritanceBehaviorSymbol, classInitializeAttributeSymbol), testContextSymbol,
                testClassAttributeSymbol, fixtureAllowInheritedTestClass: true, out bool isFixable))
        {
            context.ReportDiagnostic(isFixable
                ? methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name)
                : methodSymbol.CreateDiagnostic(Rule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));
        }
    }
}
