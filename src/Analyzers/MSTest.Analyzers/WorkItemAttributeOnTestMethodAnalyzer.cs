﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
internal sealed class WorkItemAttributeOnTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.WorkItemAttributeOnTestMethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.WorkItemAttributeOnTestMethodAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.WorkItemAttributeOnTestMethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.WorkItemAttributeOnTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out var testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingWorkItemAttribute, out var workItemAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol, workItemAttributeSymbol), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol workItemAttributeSymbol)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        AttributeData? workItemAttribute = null;
        bool hasTestMethodAttribute = false;
        foreach (var methodAttribute in methodSymbol.GetAttributes())
        {
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                hasTestMethodAttribute = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, workItemAttributeSymbol))
            {
                workItemAttribute = methodAttribute;
            }
        }

        if (workItemAttribute is not null && !hasTestMethodAttribute)
        {
            if (workItemAttribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(Rule));
            }
        }
    }
}
