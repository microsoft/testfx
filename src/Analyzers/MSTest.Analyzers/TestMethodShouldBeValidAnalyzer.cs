// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0003: <inheritdoc cref="Resources.TestMethodShouldBeValidTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestMethodShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestMethodShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestMethodShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PublicOrInternalRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_PublicOrInternal), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotStaticRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAbstractRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotAbstract), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotGenericRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor OrdinaryRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = PublicRule.WithMessage(new(nameof(Resources.TestMethodShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute, out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                INamedTypeSymbol? taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                INamedTypeSymbol? valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testMethodAttributeSymbol, taskSymbol, valueTaskSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testMethodAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
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

        if (methodSymbol is { ReturnsVoid: true, IsAsync: true })
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAsyncVoidRule, methodSymbol.Name));
        }

        if (!methodSymbol.ReturnsVoid
            && (taskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
            && (valueTaskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol)))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ReturnTypeRule, methodSymbol.Name));
        }
    }
}
