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
    internal static readonly DiagnosticDescriptor OwnerRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PriorityRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingPriorityAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingPriorityAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor TestPropertyRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor WorkItemRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingWorkItemAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingWorkItemAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DescriptionRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDescriptionAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDescriptionAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ExpectedExceptionRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AttributeOnTestMethodRuleId,
        title: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute)),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.UseAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources), GetShortAttributeName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute)),
        description: null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    // IMPORTANT: Remember to add any new rule to the rule tuple.
    private static readonly List<(string AttributeFullyQualifiedName, DiagnosticDescriptor Rule)> RuleTuples = new()
    {
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingOwnerAttribute, OwnerRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingPriorityAttribute, PriorityRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestPropertyAttribute, TestPropertyRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingWorkItemAttribute, WorkItemRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDescriptionAttribute, DescriptionRule),
        (WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingExpectedExceptionAttribute, ExpectedExceptionRule),
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = RuleTuples.Select(tuple => tuple.Rule).ToImmutableArray();

    private static string GetShortAttributeName(string attributeName) => attributeName.Split('.').Last();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                out var testMethodAttributeSymbol))
            {
                return;
            }

            List<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> attributeRuleTuples = new();
            foreach (var ruleTuple in RuleTuples)
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(ruleTuple.AttributeFullyQualifiedName, out var attributeSymbol))
                {
                    attributeRuleTuples.Add((attributeSymbol, ruleTuple.Rule));
                }
            }

            if (attributeRuleTuples.Count > 0)
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol, attributeRuleTuples), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testMethodAttributeSymbol,
        IEnumerable<(INamedTypeSymbol AttributeSymbol, DiagnosticDescriptor Rule)> attributeRuleTuples)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        List<(AttributeData AttributeData, DiagnosticDescriptor Rule)> attributes = new();
        foreach (var methodAttribute in methodSymbol.GetAttributes())
        {
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                return;
            }

            foreach (var tuple in attributeRuleTuples)
            {
                if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, tuple.AttributeSymbol))
                {
                    attributes.Add((methodAttribute, tuple.Rule));
                }
            }
        }

        foreach (var attribute in attributes)
        {
            if (attribute.AttributeData.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(attribute.Rule));
            }
        }
    }
}
