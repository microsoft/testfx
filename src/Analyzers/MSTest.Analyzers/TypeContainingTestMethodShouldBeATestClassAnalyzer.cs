// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TypeContainingTestMethodShouldBeATestClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TypeContainingTestMethodShouldBeATestClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TypeContainingTestMethodShouldBeATestClassDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TypeContainingTestMethodShouldBeATestClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor TypeContainingTestMethodShouldBeATestClassRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TypeContainingTestMethodShouldBeATestClassRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TypeContainingTestMethodShouldBeATestClassRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, testMethodAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || namedTypeSymbol.IsAbstract)
        {
            return;
        }

        bool isTestClass = false;
        foreach (AttributeData classAttribute in namedTypeSymbol.GetAttributes())
        {
            if (classAttribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestClass = true;
                break;
            }
        }

        if (isTestClass)
        {
            return;
        }

        bool hasTestMethod = false;
        INamedTypeSymbol? currentType = namedTypeSymbol;
        do
        {
            foreach (ISymbol classMember in currentType.GetMembers())
            {
                foreach (AttributeData attribute in classMember.GetAttributes())
                {
                    if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
                    {
                        hasTestMethod = true;
                        break;
                    }
                }

                if (!hasTestMethod)
                {
                    break;
                }
            }

            currentType = currentType.BaseType;
        }
        while (currentType is not null && !hasTestMethod);

        if (!hasTestMethod)
        {
            return;
        }

        context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(TypeContainingTestMethodShouldBeATestClassRule, namedTypeSymbol.Name));
    }
}
