// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0005: <inheritdoc cref="Resources.TestContextShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestContextShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestContextShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestContextShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestContextShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestContextShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PublicOrInternalRule = PublicRule.WithMessage(new(nameof(Resources.TestContextShouldBeValidMessageFormat_PublicOrInternal), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotStaticRule = PublicRule.WithMessage(new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotReadonlyRule = PublicRule.WithMessage(new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotReadonly), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotFieldRule = PublicRule.WithMessage(new(nameof(Resources.TestContextShouldBeValidMessageFormat_NotField), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out INamedTypeSymbol? testContextSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestClassAttribute, out INamedTypeSymbol? testClassAttributeSymbol))
            {
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testContextSymbol, testClassAttributeSymbol, canDiscoverInternals),
                    SymbolKind.Field, SymbolKind.Property);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testContextSymbol, INamedTypeSymbol testClassAttributeSymbol,
        bool canDiscoverInternals)
    {
        if (!context.Symbol.ContainingType.GetAttributes().Any(attr => attr.AttributeClass.Inherits(testClassAttributeSymbol)))
        {
            return;
        }

        if (context.Symbol is IFieldSymbol fieldSymbol)
        {
            AnalyzeFieldSymbol(context, fieldSymbol, testContextSymbol);
            return;
        }

        if (context.Symbol is IPropertySymbol propertySymbol)
        {
            AnalyzePropertySymbol(context, testContextSymbol, canDiscoverInternals, propertySymbol);
            return;
        }

        throw ApplicationStateGuard.Unreachable();
    }

    private static void AnalyzePropertySymbol(SymbolAnalysisContext context, INamedTypeSymbol testContextSymbol, bool canDiscoverInternals, IPropertySymbol propertySymbol)
    {
        if (propertySymbol.GetMethod is null
            || !string.Equals(propertySymbol.Name, "TestContext", StringComparison.OrdinalIgnoreCase)
            || !SymbolEqualityComparer.Default.Equals(testContextSymbol, propertySymbol.GetMethod.ReturnType))
        {
            return;
        }

        if (propertySymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && resultantVisibility != SymbolVisibility.Public)
            {
                context.ReportDiagnostic(propertySymbol.CreateDiagnostic(PublicRule));
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                context.ReportDiagnostic(propertySymbol.CreateDiagnostic(PublicOrInternalRule));
            }
        }

        if (propertySymbol.IsStatic)
        {
            context.ReportDiagnostic(propertySymbol.CreateDiagnostic(NotStaticRule));
        }

        if (propertySymbol.SetMethod is null)
        {
            context.ReportDiagnostic(propertySymbol.CreateDiagnostic(NotReadonlyRule));
        }
    }

    private static void AnalyzeFieldSymbol(SymbolAnalysisContext context, IFieldSymbol fieldSymbol, INamedTypeSymbol testContextSymbol)
    {
        if (string.Equals(fieldSymbol.Name, "TestContext", StringComparison.OrdinalIgnoreCase)
            && SymbolEqualityComparer.Default.Equals(testContextSymbol, fieldSymbol.Type))
        {
            context.ReportDiagnostic(fieldSymbol.CreateDiagnostic(NotFieldRule));
        }
    }
}
