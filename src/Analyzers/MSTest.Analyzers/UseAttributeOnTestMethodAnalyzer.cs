// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class UseAttributeOnTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor OwnerRule = CreateRule(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute);

    private static readonly Tuple<string, DiagnosticDescriptor>[] RuleTuples = new[]
    {
        new Tuple<string, DiagnosticDescriptor>(
            WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute,
            OwnerRule),
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(OwnerRule);

    private static DiagnosticDescriptor CreateRule(string attributeName)
    {
        string shortAttributeName = attributeName.Split('.').Last();

        return DiagnosticDescriptorHelper.Create(
                DiagnosticIds.AttributeOnTestMethodRuleId,
                title: new LocalizableResourceString(
                    nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), shortAttributeName),
                messageFormat: new LocalizableResourceString(
                    nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), shortAttributeName),
                description: null,
                Category.Usage,
                DiagnosticSeverity.Info,
                isEnabledByDefault: true);
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        foreach (var ruleTuple in RuleTuples)
        {
            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                        out var testMethodAttributeSymbol)
                    && context.Compilation.TryGetOrCreateTypeByMetadataName(ruleTuple.Item1, out var attributeSymbol))
                {
                    context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol, attributeSymbol, ruleTuple.Item2), SymbolKind.Method);
                }
            });
        }
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol attributeSymbol,
        DiagnosticDescriptor rule)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        AttributeData? attribute = null;
        bool hasTestMethodAttribute = false;
        foreach (var methodAttribute in methodSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, attributeSymbol))
            {
                attribute = methodAttribute;
                continue;
            }

            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                hasTestMethodAttribute = true;
            }
        }

        if (attribute is not null && !hasTestMethodAttribute)
        {
            if (attribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(rule));
            }
        }
    }
}
