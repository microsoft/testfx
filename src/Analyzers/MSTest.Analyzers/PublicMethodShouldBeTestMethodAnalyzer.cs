﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PublicMethodShouldBeTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PublicMethodShouldBeTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PublicMethodShouldBeTestMethodAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PublicMethodShouldBeTestMethodAnalyzerFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicMethodShouldBeTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PublicMethodShouldBeTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicMethodShouldBeTestMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.GetResultantVisibility() != SymbolVisibility.Public)
        {
            return;
        }

        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        bool isTestMethod = false;
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            // Check if method is a test method or inherit from the TestMethod attribute.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }
        }

        if (isTestMethod)
        {
            return;
        }

        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(PublicMethodShouldBeTestMethodRule, methodSymbol.Name));
    }
}
