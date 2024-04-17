// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassShouldHaveTestMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassShouldHaveTestMethodTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassShouldHaveTestMethodDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassShouldHaveTestMethodMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor TestClassShouldHaveTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassShouldHaveTestMethodRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestClassShouldHaveTestMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out var testClassAttributeSymbol))
            {
                var testMethodAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
                var assemblyInitializationAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyInitializeAttribute);
                var assemblyCleanupAttributeSymbol = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, testMethodAttributeSymbol, assemblyInitializationAttributeSymbol, assemblyCleanupAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol? testMethodAttributeSymbol,
        INamedTypeSymbol? assemblyInitializationAttributeSymbol, INamedTypeSymbol? assemblyCleanupAttributeSymbol)
    {
        var classSymbol = (INamedTypeSymbol)context.Symbol;

        bool isTestClass = false;
        foreach (var classAttribute in classSymbol.GetAttributes())
        {
            if (classAttribute.AttributeClass.Inherits(testClassAttributeSymbol))
            {
                isTestClass = true;
                break;
            }
        }

        if (!isTestClass)
        {
            return;
        }

        bool hasAssemblyAttribute = false;
        bool hasTestMethod = false;

        var currentType = classSymbol;
        do
        {
            foreach (var classMember in currentType.GetMembers())
            {
                foreach (var attribute in classMember.GetAttributes())
                {
                    if (attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
                    {
                        hasTestMethod = true;
                    }

                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyInitializationAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, assemblyCleanupAttributeSymbol))
                    {
                        hasAssemblyAttribute = true;
                    }
                }
            }

            currentType = currentType.BaseType;
        }
        while (currentType is not null);

        if (!hasTestMethod && (!classSymbol.IsStatic || (classSymbol.IsStatic && !hasAssemblyAttribute)))
        {
            context.ReportDiagnostic(classSymbol.CreateDiagnostic(TestClassShouldHaveTestMethodRule, classSymbol.Name));
        }
    }
}
