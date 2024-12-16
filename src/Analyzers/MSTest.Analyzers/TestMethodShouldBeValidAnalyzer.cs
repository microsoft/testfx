﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.TestMethodShouldBeValidMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor ValidTestMethodSignatureRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.TestMethodShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(ValidTestMethodSignatureRule);

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
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ValidTestMethodSignatureRule, DiagnosticDescriptorHelper.CannotFixProperties, methodSymbol.Name));

            return;
        }

        if (methodSymbol.IsGenericMethod)
        {
            foreach (ITypeParameterSymbol typeParameter in methodSymbol.TypeParameters)
            {
                // If none of the parameters match the type parameter, then that generic type can't be inferred.
                if (!methodSymbol.Parameters.Any(p => typeParameter.Equals(p.Type, SymbolEqualityComparer.Default)))
                {
                    context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ValidTestMethodSignatureRule, methodSymbol.Name));
                }
            }
        }

        if (methodSymbol.IsStatic || methodSymbol.IsAbstract || methodSymbol is { ReturnsVoid: true, IsAsync: true }
            || (!methodSymbol.ReturnsVoid
            && (taskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
            && (valueTaskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol))))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ValidTestMethodSignatureRule, methodSymbol.Name));
        }
        else if (methodSymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && (resultantVisibility != SymbolVisibility.Public || methodSymbol.DeclaredAccessibility != Accessibility.Public))
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ValidTestMethodSignatureRule, methodSymbol.Name));
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(ValidTestMethodSignatureRule, methodSymbol.Name));
            }
        }
    }
}
