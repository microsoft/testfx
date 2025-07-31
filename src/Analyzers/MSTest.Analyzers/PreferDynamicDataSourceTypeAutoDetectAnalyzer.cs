// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0052: <inheritdoc cref="Resources.PreferDynamicDataSourceTypeAutoDetectTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferDynamicDataSourceTypeAutoDetectAnalyzer : DiagnosticAnalyzer
{
    private const int DynamicDataSourceTypeProperty = 0;
    private const int DynamicDataSourceTypeMethod = 1;
    private const int DynamicDataSourceTypeAutoDetect = 2;

    private static readonly LocalizableResourceString Title = new(nameof(Resources.PreferDynamicDataSourceTypeAutoDetectTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PreferDynamicDataSourceTypeAutoDetectDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PreferDynamicDataSourceTypeAutoDetectMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PreferAutoDetectRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferDynamicDataSourceTypeAutoDetectRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(PreferAutoDetectRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataAttribute, out INamedTypeSymbol? dynamicDataAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDynamicDataSourceType, out INamedTypeSymbol? dynamicDataSourceTypeSymbol))
            {
                context.RegisterSymbolAction(
                   context => AnalyzeSymbol(context, dynamicDataAttributeSymbol, dynamicDataSourceTypeSymbol),
                   SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol dynamicDataAttributeSymbol, INamedTypeSymbol dynamicDataSourceTypeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, dynamicDataAttributeSymbol))
            {
                AnalyzeAttribute(context, methodAttribute, dynamicDataSourceTypeSymbol);
            }
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attributeData, INamedTypeSymbol dynamicDataSourceTypeSymbol)
    {
        if (attributeData.ApplicationSyntaxReference?.GetSyntax() is not { } attributeSyntax)
        {
            return;
        }

        // Check constructor arguments for explicit DynamicDataSourceType usage
        foreach (TypedConstant argument in attributeData.ConstructorArguments)
        {
            if (argument.Type is null)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(argument.Type, dynamicDataSourceTypeSymbol)
                && argument.Value is int dataSourceType)
            {
                // Flag usage of Property or Method (anything other than AutoDetect)
                if (dataSourceType is DynamicDataSourceTypeProperty or DynamicDataSourceTypeMethod)
                {
                    string sourceTypeName = dataSourceType == DynamicDataSourceTypeProperty ? "Property" : "Method";
                    context.ReportDiagnostic(attributeSyntax.CreateDiagnostic(PreferAutoDetectRule, sourceTypeName));
                }
            }
        }
    }
}