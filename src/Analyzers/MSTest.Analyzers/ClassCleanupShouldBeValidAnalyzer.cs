// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ClassCleanupShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.ClassCleanupShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.ClassCleanupShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ClassCleanupShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor StaticRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_Static), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NoParametersRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_NoParameters), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotGenericRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor OrdinaryRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAGenericClassUnlessInheritanceModeSetRule = PublicRule.WithMessage(new(nameof(Resources.ClassCleanupShouldBeValidMessageFormat_NotAGenericClassUnlessInheritanceModeSet), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
       = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassCleanupAttribute, out var classCleanupAttributeSymbol))
            {
                var taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                var inheritanceBehaviorSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior);
                var valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, classCleanupAttributeSymbol, taskSymbol, valueTaskSymbol, inheritanceBehaviorSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classCleanupAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, INamedTypeSymbol? inheritanceBehaviorSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.IsClassCleanupMethod(classCleanupAttributeSymbol))
        {
            return;
        }

        if (methodSymbol.MethodKind != MethodKind.Ordinary)
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(OrdinaryRule, methodSymbol.Name));

            // Do not check the other criteria, users should fix the method kind first.
            return;
        }

        if (context.Symbol.ContainingType.IsGenericType)
        {
            bool isInheritanceModeSet = false;
            foreach (AttributeData attr in methodSymbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classCleanupAttributeSymbol))
                {
                    continue;
                }

                ImmutableArray<TypedConstant> constructorArguments = attr.ConstructorArguments;
                foreach (var constructorArgument in constructorArguments)
                {
                    if (!SymbolEqualityComparer.Default.Equals(constructorArgument.Type, inheritanceBehaviorSymbol))
                    {
                        continue;
                    }

                    // It's an enum so it can't be null
                    RoslynDebug.Assert(constructorArgument.Value is not null);

                    // We need to check that the inheritanceBehavior is not set to none and it's value inside the enum is zero
                    if ((int)constructorArgument.Value != 0)
                    {
                        isInheritanceModeSet = true;
                        break;
                    }
                }
            }

            if (!isInheritanceModeSet)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(NotAGenericClassUnlessInheritanceModeSetRule, methodSymbol.Name));
            }
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
