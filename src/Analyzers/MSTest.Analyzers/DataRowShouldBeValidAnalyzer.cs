// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DataRowShouldBeValidAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.DataRowShouldBeValidTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.DataRowShouldBeValidDescription), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.DataRowShouldBeValidMessageFormat_OnTestMethod), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor DataRowOnTestMethodRule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.DataRowShouldBeValidRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Usage,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ArgumentCountMismatchRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_ArgumentCountMismatch), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor ArgumentTypeMismatchRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_ArgumentTypeMismatch), Resources.ResourceManager, typeof(Resources)));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
       = ImmutableArray.Create(DataRowOnTestMethodRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            // No need to register any action if we don't find the TestMethodAttribute symbol since
            // the current analyzer checks if the DataRow attribute is applied on test methods. No
            // test methods, nothing to check.
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingTestMethodAttribute,
                out var testMethodAttributeSymbol))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute,
                out var dataRowAttributeSymbol))
            {
                return;
            }

            context.RegisterSymbolAction(
                context => AnalyzeSymbol(context, testMethodAttributeSymbol, dataRowAttributeSymbol),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        INamedTypeSymbol testMethodAttributeSymbol,
        INamedTypeSymbol dataRowAttributeSymbol)
    {
        IMethodSymbol methodSymbol = (IMethodSymbol)context.Symbol;

        bool isTestMethod = false;
        List<AttributeData> dataRowAttributes = new();
        foreach (var methodAttribute in methodSymbol.GetAttributes())
        {
            // Current method should be a test method or should inherit from the TestMethod attribute.
            // If it is, the current analyzer will trigger no diagnostic so it exits.
            if (methodAttribute.AttributeClass.Inherits(testMethodAttributeSymbol))
            {
                isTestMethod = true;
            }

            if (SymbolEqualityComparer.Default.Equals(methodAttribute.AttributeClass, dataRowAttributeSymbol))
            {
                dataRowAttributes.Add(methodAttribute);
            }
        }

        // Check if attribute is set on a test method.
        if (!isTestMethod)
        {
            if (dataRowAttributes.Count > 0)
            {
                context.ReportDiagnostic(methodSymbol.CreateDiagnostic(DataRowOnTestMethodRule));
            }

            return;
        }

        // Check each data row attribute.
        foreach (var attribute in dataRowAttributes)
        {
            AnalyzeAttribute(context, attribute, methodSymbol);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attribute, IMethodSymbol methodSymbol)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not { } syntax)
        {
            return;
        }

        // No constructor arguments and no method parameters -> nothing to check.
        if (attribute.ConstructorArguments.Length == 0 && methodSymbol.Parameters.Length == 0)
        {
            return;
        }

        // Count mismatch since there's zero method parameters but there's at least one DataRow
        // constructor argument.
        if (methodSymbol.Parameters.Length == 0)
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(
                ArgumentCountMismatchRule,
                attribute.ConstructorArguments.Length,
                methodSymbol.Parameters.Length));
            return;
        }

        // Possible count mismatch depending on whether last method parameter is an array or not.
        IParameterSymbol lastMethodParameter = methodSymbol.Parameters.Last();
        bool lastMethodParameterIsArray = lastMethodParameter.Type.Kind == SymbolKind.ArrayType;
        if (attribute.ConstructorArguments.Length == 0)
        {
            if (!lastMethodParameterIsArray)
            {
                context.ReportDiagnostic(syntax.CreateDiagnostic(
                    ArgumentCountMismatchRule,
                    attribute.ConstructorArguments.Length,
                    methodSymbol.Parameters.Length));
            }

            return;
        }

        // DataRow constructors have either zero or one argument(s). If we get here, we are
        // on the one argument case. Check if we match either of the array argument constructors
        // and expand the array argument if we do.
        ImmutableArray<TypedConstant> constructorArguments = attribute.ConstructorArguments;
        if (constructorArguments[0].Kind is TypedConstantKind.Array && !constructorArguments[0].IsNull)
        {
            constructorArguments = constructorArguments[0].Values;
        }

        bool lastMethodParameterIsParams = lastMethodParameter.IsParams;
        bool uniqueMethodParameter = methodSymbol.Parameters.Length == 1;
        bool hasDefaultValue = lastMethodParameter.HasExplicitDefaultValue;
        bool strictMatch =
            !lastMethodParameterIsArray
            || (lastMethodParameterIsArray
                && !lastMethodParameterIsParams
                && !uniqueMethodParameter
                && !hasDefaultValue);

        if (IsArgumentCountMismatch(constructorArguments.Length, methodSymbol.Parameters.Length, strictMatch))
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(
                ArgumentCountMismatchRule,
                constructorArguments.Length,
                methodSymbol.Parameters.Length));
            return;
        }

        // Check constructor argument types match method parameter types.
        List<(int ConstructorArgumentIndex, int MethodParameterIndex)> typeMismatchIndices = new();
        for (int i = 0; i < constructorArguments.Length; ++i)
        {
            // Null is considered as default for non-nullable types.
            if (constructorArguments[i].IsNull)
            {
                continue;
            }

            ITypeSymbol? argumentType = constructorArguments[i].Type;
            ITypeSymbol paramType = (lastMethodParameterIsArray && i >= methodSymbol.Parameters.Length - 1)
                ? ((IArrayTypeSymbol)lastMethodParameter.Type).ElementType
                : methodSymbol.Parameters[i].Type;

            if (argumentType is not null && !argumentType.IsAssignableTo(paramType, context.Compilation))
            {
                typeMismatchIndices.Add((i, Math.Min(i, methodSymbol.Parameters.Length - 1)));
            }
        }

        // Report diagnostics if there's any type mismatch.
        if (typeMismatchIndices.Count > 0)
        {
            context.ReportDiagnostic(syntax.CreateDiagnostic(
                ArgumentTypeMismatchRule,
                string.Join(", ", typeMismatchIndices)));
        }
    }

    private static bool IsArgumentCountMismatch(int constructorArgumentsLength, int methodParametersLength, bool strictMatch)
    {
        // 1. If last method parameter is not an array the lengths must be the same.
        // 2. If last method parameter is an array the argument count check is relaxed and we only
        //    need to make sure we don't have less constructor arguments than actual method paramters.
        return strictMatch
            ? constructorArgumentsLength != methodParametersLength
            : constructorArgumentsLength < methodParametersLength - 1;
    }
}
