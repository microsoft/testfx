// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0015: <inheritdoc cref="Resources.TestMethodShouldNotBeIgnoredAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodShouldNotBeIgnoredAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestMethodShouldNotBeIgnoredAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestMethodShouldNotBeIgnoredAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestMethodShouldNotBeIgnoredAnalyzerFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor TestMethodShouldNotBeIgnoredRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestMethodShouldNotBeIgnoredRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(TestMethodShouldNotBeIgnoredRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingIgnoreAttribute, out INamedTypeSymbol? ignoreAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, ignoreAttributeSymbol, testMethodAttributeSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol ignoreAttributeSymbol, INamedTypeSymbol testMethodAttributeSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        ImmutableArray<AttributeData> methodAttributes = methodSymbol.GetAttributes();
        bool isTestMethod = false;
        bool isMethodIgnored = false;
        foreach (AttributeData methodAttribute in methodAttributes)
        {
            // Current method should be a test method or should inherit from the TestMethod attribute.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, ignoreAttributeSymbol))
            {
                isMethodIgnored = true;
            }
        }

        if (!isTestMethod || !isMethodIgnored)
        {
            return;
        }

        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(TestMethodShouldNotBeIgnoredRule, methodSymbol.Name));
    }
}
