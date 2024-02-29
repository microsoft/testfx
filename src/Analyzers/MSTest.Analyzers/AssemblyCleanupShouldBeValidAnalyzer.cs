// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class AssemblyCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.AssemblyCleanupShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.AssemblyCleanupShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.AssemblyCleanupShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor StaticRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_Static), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NoParametersRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_NoParameters), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotGenericRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor OrdinaryRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAGenericClassRule = PublicRule.WithMessage(new(nameof(Resources.AssemblyCleanupShouldBeValidMessageFormat_NotAGenericClass), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
       = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssemblyCleanupAttribute, out var assemblyCleanupAttributeSymbol))
            {
                var taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                var valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, assemblyCleanupAttributeSymbol, taskSymbol, valueTaskSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol assemblyCleanupAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var containingTypeSymbol = context.Symbol.ContainingType;

        if (!methodSymbol.IsAssemblyCleanupMethod(assemblyCleanupAttributeSymbol))
        {
            return;
        }

        if (methodSymbol.MethodKind != MethodKind.Ordinary)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(OrdinaryRule, methodSymbol.Name));

            // Do not check the other criteria, users should fix the method kind first.
            return;
        }

        if (containingTypeSymbol.IsGenericType)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAGenericClassRule, methodSymbol.Name));
        }

        if (methodSymbol.Parameters.Length > 0)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NoParametersRule, methodSymbol.Name));
        }

        if (methodSymbol.IsGenericMethod)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotGenericRule, methodSymbol.Name));
        }

        if (!methodSymbol.IsStatic)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(StaticRule, methodSymbol.Name));
        }

        if (methodSymbol.ReturnsVoid && methodSymbol.IsAsync)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAsyncVoidRule, methodSymbol.Name));
        }

        if (!methodSymbol.IsPublicAndHasCorrectResultantVisibility(canDiscoverInternals))
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
