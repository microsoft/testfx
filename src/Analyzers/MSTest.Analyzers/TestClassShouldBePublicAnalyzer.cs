﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassShouldBePublicAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassShouldBePublicTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassShouldBePublicMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassShouldBePublicDescription), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.TestClassShouldBePublicRuleId,
        Title,
        MessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestClassShouldBePublicRuleId}.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

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
            || namedTypeSymbol.GetResultantVisibility() == SymbolVisibility.Public)
        {
            return;
        }

        if (namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule, namedTypeSymbol.Name));
        }
    }
}
