// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString PublicMessageFormat = new(nameof(Resources.TestClassShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor PublicRule = new(
        DiagnosticIds.TestClassShouldBeValidRuleId,
        Title,
        PublicMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestClassShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotStaticMessageFormat = new(nameof(Resources.TestClassShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotStaticRule = new(
        DiagnosticIds.TestClassShouldBeValidRuleId,
        Title,
        NotStaticMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestClassShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotGenericMessageFormat = new(nameof(Resources.TestClassShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotGenericRule = new(
        DiagnosticIds.TestClassShouldBeValidRuleId,
        Title,
        NotGenericMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestClassShouldBeValidRuleId}.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out var testClassAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testClassAttributeSymbol), SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || !namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        if (namedTypeSymbol.GetResultantVisibility() != SymbolVisibility.Public)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(PublicRule, namedTypeSymbol.Name));
        }

        if (namedTypeSymbol.IsGenericType)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(NotGenericRule, namedTypeSymbol.Name));
        }

        if (namedTypeSymbol.IsStatic)
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(NotStaticRule, namedTypeSymbol.Name));
        }
    }
}
