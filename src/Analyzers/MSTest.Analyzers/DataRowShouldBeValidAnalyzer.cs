// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0014: <inheritdoc cref="Resources.DataRowShouldBeValidTitle"/>.
/// </summary>
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
                out INamedTypeSymbol? testMethodAttributeSymbol))
            {
                return;
            }

            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(
                WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingDataRowAttribute,
                out INamedTypeSymbol? dataRowAttributeSymbol))
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
        var methodSymbol = (IMethodSymbol)context.Symbol;

        bool isTestMethod = false;
        List<AttributeData> dataRowAttributes = new();
        foreach (AttributeData methodAttribute in methodSymbol.GetAttributes())
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
        foreach (AttributeData attribute in dataRowAttributes)
        {
            AnalyzeAttribute(context, attribute, methodSymbol);
        }
    }

    private static void AnalyzeAttribute(SymbolAnalysisContext context, AttributeData attribute, IMethodSymbol methodSymbol)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not { } dataRowSyntax)
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
            context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(
                ArgumentCountMismatchRule,
                attribute.ConstructorArguments.Length,
                methodSymbol.Parameters.Length));
            return;
        }

        // Possible count mismatch depending on whether last method parameter is an array or not.
        if (attribute.ConstructorArguments.Length == 0)
        {
            if (methodSymbol.Parameters[^1].Type.Kind != SymbolKind.ArrayType)
            {
                context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(
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
        if (attribute.AttributeConstructor?.Parameters.FirstOrDefault()?.IsParams == true)
        {
            constructorArguments = constructorArguments[0].Values;
        }

        if (IsArgumentCountMismatch(constructorArguments.Length, methodSymbol.Parameters))
        {
            context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(
                ArgumentCountMismatchRule,
                constructorArguments.Length,
                methodSymbol.Parameters.Length));
            return;
        }

        // Check constructor argument types match method parameter types.
        List<(int ConstructorArgumentIndex, int MethodParameterIndex)> typeMismatchIndices = new();
        for (int currentArgumentIndex = 0; currentArgumentIndex < constructorArguments.Length; currentArgumentIndex++)
        {
            // Null is considered as default for non-nullable types.
            if (constructorArguments[currentArgumentIndex].IsNull)
            {
                continue;
            }

            ITypeSymbol? argumentType = constructorArguments[currentArgumentIndex].Type;
            ITypeSymbol paramType = GetParameterType(methodSymbol, currentArgumentIndex, constructorArguments.Length);

            if (argumentType is not null && !argumentType.IsAssignableTo(paramType, context.Compilation))
            {
                typeMismatchIndices.Add((currentArgumentIndex, Math.Min(currentArgumentIndex, methodSymbol.Parameters.Length - 1)));
            }
        }

        // Report diagnostics if there's any type mismatch.
        if (typeMismatchIndices.Count > 0)
        {
            context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(
                ArgumentTypeMismatchRule,
                string.Join(", ", typeMismatchIndices)));
        }
    }

    private static ITypeSymbol GetParameterType(IMethodSymbol methodSymbol, int currentArgumentIndex, int argumentsCount)
    {
        if (currentArgumentIndex >= methodSymbol.Parameters.Length - 1)
        {
            IParameterSymbol lastParameter = methodSymbol.Parameters[^1];

            // When last parameter is params, we want to check that the extra arguments match the type of the array
            if (lastParameter.IsParams)
            {
                return ((IArrayTypeSymbol)lastParameter.Type).ElementType;
            }

            // When only parameter is array and we have more than one argument, we want to check the array type
            if (argumentsCount > 1 && methodSymbol.Parameters.Length == 1 && lastParameter.Type.Kind == SymbolKind.ArrayType)
            {
                return ((IArrayTypeSymbol)lastParameter.Type).ElementType;
            }
        }

        return methodSymbol.Parameters[currentArgumentIndex].Type;
    }

    private static bool IsArgumentCountMismatch(int constructorArgumentsLength, ImmutableArray<IParameterSymbol> methodParameters)
    {
        int optionalParametersCount = methodParameters.Count(x => x.HasExplicitDefaultValue);
        bool isLastParameterParams = methodParameters[^1].IsParams;
        bool isOnlyParameterAndIsArray = methodParameters is [{ Type.Kind: SymbolKind.ArrayType }];

        if (isOnlyParameterAndIsArray)
        {
            return false;
        }

        // When there is a params parameter, we should only check if the minimal number of arguments is matched.
        if (isLastParameterParams)
        {
            return constructorArgumentsLength < methodParameters.Length - 1 /* params can be empty */ - optionalParametersCount;
        }

        // When there are some optional parameters (and no params), we are invalid if:
        // - there are too many arguments
        // - less than non-optional parameters
        if (optionalParametersCount > 0)
        {
            return constructorArgumentsLength > methodParameters.Length
                || constructorArgumentsLength < methodParameters.Length - optionalParametersCount;
        }

        // Strict check
        return constructorArgumentsLength != methodParameters.Length;
    }
}
