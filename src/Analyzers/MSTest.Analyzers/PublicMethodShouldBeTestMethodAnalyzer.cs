// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0029: <inheritdoc cref="Resources.PublicMethodShouldBeTestMethodAnalyzerTitle"/>.
/// </summary>
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
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out INamedTypeSymbol? testInitializeAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute, out INamedTypeSymbol? testCleanupAttributeSymbol))
            {
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, testClassAttributeSymbol, testInitializeAttributeSymbol, testCleanupAttributeSymbol, taskSymbol, valueTaskSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol testClassAttributeSymbol, INamedTypeSymbol testInitializeAttributeSymbol, INamedTypeSymbol testCleanupAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (methodSymbol.GetResultantVisibility() != SymbolVisibility.Public)
        {
            return;
        }

        if (!methodSymbol.HasValidTestMethodSignature(taskSymbol, valueTaskSymbol, canDiscoverInternals))
        {
            return;
        }

        INamedTypeSymbol containingTypeSymbol = context.Symbol.ContainingType;
        bool isTestClass = false;
        foreach (AttributeData classAttribute in containingTypeSymbol.GetAttributes())
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

        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        // check if the method has testMethod, testInitialize or testCleanup attribute
        bool hasValidAttribute = false;
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            // Check if method is a test method or inherit from the TestMethod attribute.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol)
                || SymbolEqualityComparer.Default.Equals(testMethodAttributeSymbol, testInitializeAttributeSymbol)
                || SymbolEqualityComparer.Default.Equals(testMethodAttributeSymbol, testCleanupAttributeSymbol))
            {
                hasValidAttribute = true;
            }
        }

        if (hasValidAttribute)
        {
            return;
        }

        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(PublicMethodShouldBeTestMethodRule, methodSymbol.Name));
    }
}
