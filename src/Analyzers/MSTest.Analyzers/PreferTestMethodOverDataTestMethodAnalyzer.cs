// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0044: <inheritdoc cref="Resources.PreferTestMethodOverDataTestMethodAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PreferTestMethodOverDataTestMethodAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor PreferTestMethodOverDataTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PreferTestMethodOverDataTestMethodRuleId,
        title: new LocalizableResourceString(nameof(Resources.PreferTestMethodOverDataTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources)),
        messageFormat: new LocalizableResourceString(nameof(Resources.PreferTestMethodOverDataTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources)),
        description: new LocalizableResourceString(nameof(Resources.PreferTestMethodOverDataTestMethodAnalyzerDescription), Resources.ResourceManager, typeof(Resources)),
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PreferTestMethodOverDataTestMethodRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataTestMethodAttribute, out INamedTypeSymbol? dataTestMethodAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(context => AnalyzeMethod(context, dataTestMethodAttributeSymbol), SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol dataTestMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        foreach (AttributeData attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(dataTestMethodAttributeSymbol))
            {
                if (attribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
                {
                    context.ReportDiagnostic(syntax.CreateDiagnostic(PreferTestMethodOverDataTestMethodRule));
                }
            }
        }
    }
}