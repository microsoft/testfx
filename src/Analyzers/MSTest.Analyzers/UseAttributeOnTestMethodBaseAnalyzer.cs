// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public abstract class UseAttributeOnTestMethodBaseAnalyzer : DiagnosticAnalyzer
{
    private readonly DiagnosticDescriptor _rule;
    private readonly string _attributeFullName;

    protected UseAttributeOnTestMethodBaseAnalyzer(DiagnosticDescriptor rule, string attributeFullName)
    {
        _rule = rule;
        _attributeFullName = attributeFullName;
    }

    public sealed override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out var testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(_attributeFullName, out var attributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol, attributeSymbol, _rule), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol attributeSymbol, DiagnosticDescriptor rule)
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
