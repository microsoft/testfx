// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DoNotUseSystemDescriptionAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DoNotUseSystemDescriptionAttributeTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DoNotUseSystemDescriptionAttributeDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DoNotUseSystemDescriptionAttributeMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DoNotUseSystemDescriptionAttributeRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DoNotUseSystemDescriptionAttributeRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(DoNotUseSystemDescriptionAttributeRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDescriptionAttribute, out INamedTypeSymbol? systemDescriptionAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, systemDescriptionAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol systemDescriptionAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool hasTestMethodAttribute = false;
        bool hasSystemDescriptionAttribute = false;
        foreach (AttributeData attribute in methodSymbol.GetAttributes())
        {
            if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                hasTestMethodAttribute = true;
            }

            if (SymbolEqualityComparer.Default.Equals((attribute, systemDescriptionAttributeSymbol))
            {
                hasSystemDescriptionAttribute = true;
            }

            if (!hasTestMethodAttribute && !hasSystemDescriptionAttribute)
            {
                break;
            }
        }

        if (!hasTestMethodAttribute && !hasSystemDescriptionAttribute)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(DoNotUseSystemDescriptionAttributeRule, methodSymbol.Name));
        }
    }
}
