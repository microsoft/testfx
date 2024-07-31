// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0034: <inheritdoc cref="Resources.UseDeploymentItemWithTestMethodOrTestClassTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseDeploymentItemWithTestMethodOrTestClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseDeploymentItemWithTestMethodOrTestClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.UseDeploymentItemWithTestMethodOrTestClassDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseDeploymentItemWithTestMethodOrTestClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseDeploymentItemWithTestMethodOrTestClassRule = DiagnosticDescriptorHelper.Create(
        id: DiagnosticIds.UseDeploymentItemWithTestMethodOrTestClassRuleId,
        Title,
        MessageFormat,
        Description,
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
                    SymbolKind.NamedType, SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol deploymentItemAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class && context.Symbol is not IMethodSymbol)
        {
            return;
        }

        bool hasDeploymentItemAttribute = false;
        bool isTestMethodOrTestClass = false;
        foreach (AttributeData attribute in namedTypeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(testClassAttributeSymbol)
                || attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethodOrTestClass = true;
            }

            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, deploymentItemAttributeSymbol))
            {
                hasDeploymentItemAttribute = true;
            }
        }

        if (hasDeploymentItemAttribute && !isTestMethodOrTestClass)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(UseDeploymentItemWithTestMethodOrTestClassRule));
        }
    }
}
