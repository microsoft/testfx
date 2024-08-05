// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0002: <inheritdoc cref="Resources.TestClassShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestClassShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestClassShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestClassShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestClassShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PublicOrInternalRule = PublicRule.WithMessage(new(nameof(Resources.TestClassShouldBeValidMessageFormat_PublicOrInternal), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotStaticRule = PublicRule.WithMessage(new(nameof(Resources.TestClassShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                INamedTypeSymbol? testMethodAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute);
                INamedTypeSymbol? testInitializeAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute);
                INamedTypeSymbol? testCleanupAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestCleanupAttribute);
                INamedTypeSymbol? classInitializeAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute);
                INamedTypeSymbol? classCleanupAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testClassAttributeSymbol, canDiscoverInternals, testMethodAttributeSymbol,
                        testInitializeAttributeSymbol, testCleanupAttributeSymbol, classInitializeAttributeSymbol, classCleanupAttributeSymbol),
                    SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testClassAttributeSymbol, bool canDiscoverInternals,
        INamedTypeSymbol? testMethodAttributeSymbol, INamedTypeSymbol? testInitializeAttributeSymbol, INamedTypeSymbol? testCleanupAttributeSymbol,
        INamedTypeSymbol? classInitializeAttributeSymbol, INamedTypeSymbol? classCleanupAttributeSymbol)
    {
        var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;
        if (namedTypeSymbol.TypeKind != TypeKind.Class
            || !namedTypeSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testClassAttributeSymbol)))
        {
            return;
        }

        if (namedTypeSymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && resultantVisibility != SymbolVisibility.Public)
            {
                context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(PublicRule, namedTypeSymbol.Name));
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(PublicOrInternalRule, namedTypeSymbol.Name));
            }
        }

        if (namedTypeSymbol.IsStatic)
        {
            foreach (ISymbol member in namedTypeSymbol.GetMembers())
            {
                if (member.Kind != SymbolKind.Method)
                {
                    continue;
                }

                var method = (IMethodSymbol)member;
                if (method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                foreach (AttributeData attribute in method.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testInitializeAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, testCleanupAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, classInitializeAttributeSymbol)
                        || SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, classCleanupAttributeSymbol)
                        || attribute.AttributeClass.Inherits(testMethodAttributeSymbol))
                    {
                        context.ReportDiagnostic(namedTypeSymbol.CreateDiagnostic(NotStaticRule, namedTypeSymbol.Name));

                        // We only need to report once per class.
                        break;
                    }
                }
            }
        }
    }
}
