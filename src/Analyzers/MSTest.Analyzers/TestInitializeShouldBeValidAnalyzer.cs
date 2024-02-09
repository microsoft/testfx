// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class TestInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.TestInitializeShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.TestInitializeShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestClassShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NotStaticRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_NotStatic), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NoParametersRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_NoParameters), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAbstractRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_NotAbstract), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotGenericRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor OrdinaryRule = PublicRule.WithMessage(new(nameof(Resources.TestInitializeShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
       = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestInitializeAttribute, out var testInitializeAttributeSymbol))
            {
                var taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                var valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, testInitializeAttributeSymbol, taskSymbol, valueTaskSymbol),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol testInitializeAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        if (!methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testInitializeAttributeSymbol)))
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

        if (methodSymbol.IsGenericMethod)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotGenericRule, methodSymbol.Name));
        }

        if (methodSymbol.Parameters.Length > 0)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NoParametersRule, methodSymbol.Name));
        }

        if (methodSymbol.IsStatic)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotStaticRule, methodSymbol.Name));
        }

        if (methodSymbol.ReturnsVoid && methodSymbol.IsAsync)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAsyncVoidRule, methodSymbol.Name));
        }

        if (methodSymbol.GetResultantVisibility() != SymbolVisibility.Public || methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(PublicRule, methodSymbol.Name));
        }

        if (!methodSymbol.ReturnsVoid
            && (taskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
            && (valueTaskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol)))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ReturnTypeRule, methodSymbol.Name));
        }
    }
}
