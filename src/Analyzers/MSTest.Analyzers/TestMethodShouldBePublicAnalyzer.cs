// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodShouldBePublicAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestMethodShouldBePublicTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestMethodShouldBePublicMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestMethodShouldBePublicDescription), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.TestMethodShouldBePublicRuleId,
        Title,
        MessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBePublicRuleId}.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out var testMethodAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.MethodKind != MethodKind.Ordinary
            || (methodSymbol.GetResultantVisibility() == SymbolVisibility.Public && methodSymbol.DeclaredAccessibility == Accessibility.Public))
        {
            return;
        }

        if (methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testMethodAttributeSymbol)))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule, methodSymbol.Name));
        }
    }
}
