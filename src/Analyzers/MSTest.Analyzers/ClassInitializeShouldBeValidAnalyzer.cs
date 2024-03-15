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
public sealed class ClassInitializeShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.ClassInitializeShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.ClassInitializeShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_Public), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor PublicRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ClassInitializeShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor StaticRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_Static), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor SingleContextParameterRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_SingleContextParameter), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor ReturnTypeRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_ReturnType), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAsyncVoidRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_NotAsyncVoid), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotGenericRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_NotGeneric), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor OrdinaryRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_Ordinary), Resources.ResourceManager, typeof(Resources)));
    internal static readonly DiagnosticDescriptor NotAGenericClassUnlessInheritanceModeSetRule = PublicRule.WithMessage(new(nameof(Resources.ClassInitializeShouldBeValidMessageFormat_NotAGenericClassUnlessInheritanceModeSet), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(PublicRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingClassInitializeAttribute, out var classInitializeAttributeSymbol)
                && context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestContext, out var testContextSymbol))
            {
                var taskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksTask);
                var inheritanceBehaviorSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingInheritanceBehavior);
                var valueTaskSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
                bool canDiscoverInternals = context.Compilation.CanDiscoverInternals();
                context.RegisterSymbolAction(
                    context => AnalyzeSymbol(context, classInitializeAttributeSymbol, taskSymbol, valueTaskSymbol, testContextSymbol, inheritanceBehaviorSymbol, canDiscoverInternals),
                    SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol classInitializeAttributeSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, INamedTypeSymbol? testContextSymbol, INamedTypeSymbol? inheritanceBehaviorSymbol, bool canDiscoverInternals)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;

        if (!methodSymbol.IsClassInitializeMethod(classInitializeAttributeSymbol))
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
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classInitializeAttributeSymbol))
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

        if (methodSymbol.Parameters.Length != 1 || testContextSymbol is null ||
            !SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, testContextSymbol))
        {
            context.ReportDiagnostic(methodSymbol.CreateDiagnostic(SingleContextParameterRule, methodSymbol.Name));
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
