// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0035: <inheritdoc cref="Resources.UseDeploymentItemWithTestMethodOrTestClassTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseDeploymentItemWithTestMethodOrTestClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseDeploymentItemWithTestMethodOrTestClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseDeploymentItemWithTestMethodOrTestClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseDeploymentItemWithTestMethodOrTestClassRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseDeploymentItemWithTestMethodOrTestClassRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseDeploymentItemWithTestMethodOrTestClassRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                 && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDeploymentItemAttribute, out INamedTypeSymbol? deploymentItemAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, testClassAttributeSymbol, deploymentItemAttributeSymbol),
                    new[] { SymbolKind.NamedType, SymbolKind.Method });
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol deploymentItemAttributeSymbol)
    {
        bool hasDeploymentItemAttribute = false;
        bool isTestMethodOrTestClass = false;
        foreach (AttributeData attribute in context.Symbol.GetAttributes())
        {
            if (context.Symbol.Kind == SymbolKind.NamedType && attribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestMethodOrTestClass = true;
            }
            else if (context.Symbol.Kind == SymbolKind.Method && attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethodOrTestClass = true;
            }
            else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, deploymentItemAttributeSymbol))
            {
                hasDeploymentItemAttribute = true;
            }
        }

        if (hasDeploymentItemAttribute && !isTestMethodOrTestClass)
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(UseDeploymentItemWithTestMethodOrTestClassRule));
        }
    }
}
