// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0052: <inheritdoc cref="Resources.AvoidExplicitDynamicDataSourceTypeTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AvoidExplicitDynamicDataSourceTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AvoidExplicitDynamicDataSourceTypeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AvoidExplicitDynamicDataSourceTypeMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PreferAutoDetectRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AvoidExplicitDynamicDataSourceTypeRuleId,
        Title,
        MessageFormat,
        null,
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
        if (attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken) is { } syntax &&
            attributeData.AttributeConstructor?.Parameters.Any(p => dynamicDataSourceTypeSymbol.Equals(p.Type, SymbolEqualityComparer.Default)) == true)
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(PreferAutoDetectRule));
        }
    }
}
