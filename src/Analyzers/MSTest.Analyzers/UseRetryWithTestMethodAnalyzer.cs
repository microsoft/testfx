// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0035: <inheritdoc cref="Resources.UseRetryWithTestMethodTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseRetryWithTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseRetryWithTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseRetryWithTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor UseRetryWithTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseRetryWithTestMethodRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        escalateToErrorInRecommended: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(UseRetryWithTestMethodRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol) &&
                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingRetryBaseAttribute, out INamedTypeSymbol? retryBaseAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, retryBaseAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol retryBaseAttributeSymbol)
    {
        bool hasRetryBaseAttribute = false;
        foreach (AttributeData attribute in context.Symbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                // We are looking for retry attributes that are not applied to test methods.
                // If this is already a test method, we just return.
                return;
            }

            if (attribute.AttributeClass.Inherits(retryBaseAttributeSymbol))
            {
                hasRetryBaseAttribute = true;
            }
        }

        // We looked all attributes, and we found a retry attribute, but didn't find TestMethodAttribute.
        if (hasRetryBaseAttribute)
        {
            context.ReportDiagnostic(context.Symbol.CreateDiagnostic(UseRetryWithTestMethodRule));
        }
    }
}
