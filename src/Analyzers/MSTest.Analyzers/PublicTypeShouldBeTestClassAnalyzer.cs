// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class PublicTypeShouldBeTestClassAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.PublicTypeShouldBeTestClassTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.PublicTypeShouldBeTestClassDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.PublicTypeShouldBeTestClassMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.PublicTypeShouldBeTestClassRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testClassAttributeSymbol), SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.DeclaredAccessibility != Accessibility.Public
            || namedTypeSymbol.GetResultantVisibility() != SymbolVisibility.Public)
        {
            return;
        }

        // The type is public, ensure this is a test class.
        if (!namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(Rule, namedTypeSymbol.Name));
        }
    }
}
