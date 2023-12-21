// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestMethodShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestMethodShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString PublicMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor PublicRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        PublicMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString PublicOrInternalMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_PublicOrInternal), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor PublicOrInternalRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        PublicOrInternalMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotStaticMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotStaticRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        NotStaticMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotAbstractMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotAbstract), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotAbstractRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        NotAbstractMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotGenericMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotGenericRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        NotGenericMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString OrdinaryMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor OrdinaryRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        OrdinaryMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString ReturnTypeMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        ReturnTypeMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    private static readonly LocalizableResourceString NotAsyncVoidMessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = new(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        NotAsyncVoidMessageFormat,
        Categories.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        Description,
        $"https://github.com/microsoft/testfx/blob/main/docs/analyzers/{DiagnosticIds.TestMethodShouldBeValidRuleId}.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out var testMethodAttributeSymbol))
            {
                var taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                bool canDiscoverInternals = context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDiscoverInternalsAttribute, out var discoverInternalsAttributeSymbol)
                    && context.Compilation.Assembly.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, discoverInternalsAttributeSymbol));

                context.RegisterSymbolAction(context => AnalyzeSymbol(context, testMethodAttributeSymbol, taskSymbol, canDiscoverInternals), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol? taskSymbol,
        bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (!methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testMethodAttributeSymbol)))
        {
            return;
        }

        if (methodSymbol.MethodKind != MethodKind.Ordinary)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(OrdinaryRule, methodSymbol.Name));

            // Do not check the other criteria, users should fix the method kind first.
            return;
        }

        if (methodSymbol.IsAbstract)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAbstractRule, methodSymbol.Name));
        }

        if (methodSymbol.IsStatic)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotStaticRule, methodSymbol.Name));
        }

        if (methodSymbol.IsGenericMethod)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotGenericRule, methodSymbol.Name));
        }

        if (methodSymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && (resultantVisibility != SymbolVisibility.Public || methodSymbol.DeclaredAccessibility != Accessibility.Public))
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(PublicRule, methodSymbol.Name));
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(PublicOrInternalRule, methodSymbol.Name));
            }
        }

        if (methodSymbol.ReturnsVoid && methodSymbol.IsAsync)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAsyncVoidRule, methodSymbol.Name));
        }

        if (!methodSymbol.ReturnsVoid
            && (taskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol)))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ReturnTypeRule, methodSymbol.Name));
        }
    }
}
