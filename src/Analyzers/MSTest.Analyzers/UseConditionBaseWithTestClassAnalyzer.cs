// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0041: <inheritdoc cref="Resources.UseConditionBaseWithTestClassTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseConditionBaseWithTestClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseConditionBaseWithTestClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseConditionBaseWithTestClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseConditionBaseWithTestClassRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseConditionBaseWithTestClassRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseConditionBaseWithTestClassRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingConditionBaseAttribute, out INamedTypeSymbol? conditionBaseAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, conditionBaseAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol conditionBaseAttributeSymbol)
    {
        INamedTypeSymbol? conditionBaseAttribute = null;
        bool isTestClass = false;
        foreach (AttributeData attribute in context.Symbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestClass = true;
            }
            else if (attribute.AttributeClass.Inherits(conditionBaseAttributeSymbol))
            {
                conditionBaseAttribute = attribute.AttributeClass;
            }
        }

        if (conditionBaseAttribute is not null && !isTestClass)
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(UseConditionBaseWithTestClassRule, conditionBaseAttribute.Name));
        }
    }
}
