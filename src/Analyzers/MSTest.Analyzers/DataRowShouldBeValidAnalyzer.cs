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

    internal static readonly DiagnosticDescriptor GenericTypeArgumentNotResolvedRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_GenericTypeArgumentNotResolved), Resources.ResourceManager, typeof(Resources)));

    internal static readonly DiagnosticDescriptor GenericTypeArgumentConflictingTypesRule = DataRowOnTestMethodRule
        .WithMessage(new(nameof(Resources.DataRowShouldBeValidMessageFormat_GenericTypeArgumentConflictingTypes), Resources.ResourceManager, typeof(Resources)));

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(
            DataRowOnTestMethodRule,
            ArgumentCountMismatchRule,
            ArgumentTypeMismatchRule,
            GenericTypeArgumentNotResolvedRule,
            GenericTypeArgumentConflictingTypesRule);

    /// <inheritdoc />
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
        List<AttributeData> dataRowAttributes = [];
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

        AnalyzeGenericMethod(context, dataRowSyntax, methodSymbol, constructorArguments);

        // Check constructor argument types match method parameter types.
        List<(string ParameterName, string ExpectedType, string ActualType)> typeMismatches = [];
        for (int currentArgumentIndex = 0; currentArgumentIndex < constructorArguments.Length; currentArgumentIndex++)
        {
            // Null is considered as default for non-nullable types.
            if (constructorArguments[currentArgumentIndex].IsNull)
            {
                continue;
            }

            ITypeSymbol? argumentType = constructorArguments[currentArgumentIndex].Type;
            ITypeSymbol paramType = GetParameterType(methodSymbol.Parameters, currentArgumentIndex, constructorArguments.Length);
            if (paramType.TypeKind == TypeKind.TypeParameter ||
                paramType is IArrayTypeSymbol { ElementType.TypeKind: TypeKind.TypeParameter })
            {
                // That means the actual type cannot be determined. We should have issued a separate
                // diagnostic for that in AnalyzeGenericMethod call above.
                continue;
            }

            if (argumentType is not null && !argumentType.IsAssignableTo(paramType, context.Compilation))
            {
                int parameterIndex = Math.Min(currentArgumentIndex, methodSymbol.Parameters.Length - 1);
                string parameterName = methodSymbol.Parameters[parameterIndex].Name;
                string expectedType = paramType.ToDisplayString();
                string actualType = argumentType.ToDisplayString();
                typeMismatches.Add((parameterName, expectedType, actualType));
            }
        }

        // Report diagnostics if there's any type mismatch.
        if (typeMismatches.Count > 0)
        {
            // Format all mismatches into a single message
            string mismatchMessage;
            if (typeMismatches.Count == 1)
            {
                (string parameterName, string expectedType, string actualType) = typeMismatches[0];
                mismatchMessage = string.Format(CultureInfo.InvariantCulture, Resources.DataRowShouldBeValidMessageFormat_ParameterMismatch, parameterName, expectedType, actualType);
            }
            else
            {
                IEnumerable<string> mismatchDescriptions = typeMismatches.Select(m =>
                    string.Format(CultureInfo.InvariantCulture, Resources.DataRowShouldBeValidMessageFormat_ParameterMismatch, m.ParameterName, m.ExpectedType, m.ActualType));
                mismatchMessage = string.Join("; ", mismatchDescriptions);
            }

            context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(
                ArgumentTypeMismatchRule,
                mismatchMessage));
        }
    }

    private static Type GetSystemType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            if (((INamedTypeSymbol)type).EnumUnderlyingType is { } underlyingType)
            {
                type = underlyingType;
            }
            else
            {
                // If this is reachable, it will be an error scenario.
                return typeof(int);
            }
        }

        return type.SpecialType switch
        {
            SpecialType.System_Boolean => typeof(bool),
            SpecialType.System_Byte => typeof(byte),
            SpecialType.System_Char => typeof(char),
            SpecialType.System_Decimal => typeof(decimal),
            SpecialType.System_Double => typeof(double),
            SpecialType.System_Int16 => typeof(short),
            SpecialType.System_Int32 => typeof(int),
            SpecialType.System_Int64 => typeof(long),
            SpecialType.System_IntPtr => typeof(IntPtr),
            SpecialType.System_SByte => typeof(sbyte),
            SpecialType.System_Single => typeof(float),
            SpecialType.System_String => typeof(string),
            SpecialType.System_UInt16 => typeof(ushort),
            SpecialType.System_UInt32 => typeof(uint),
            SpecialType.System_UInt64 => typeof(ulong),
            SpecialType.System_UIntPtr => typeof(UIntPtr),
            // All types that can be constants should hopefully be handled above.
            _ => throw new ArgumentException($"Unexpected SpecialType '{type.SpecialType}'."),
        };
    }

    private static void AnalyzeGenericMethod(
        SymbolAnalysisContext context,
        SyntaxNode dataRowSyntax,
        IMethodSymbol methodSymbol,
        ImmutableArray<TypedConstant> constructorArguments)
    {
        if (!methodSymbol.IsGenericMethod)
        {
            return;
        }

        var parameterTypesSubstitutions = new Dictionary<ITypeSymbol, (ITypeSymbol Symbol, Type SystemType)>(SymbolEqualityComparer.Default);
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            TypedConstant constructorArgument = constructorArguments[parameter.Ordinal];

            // This happens for [DataRow(null)] which ends up being resolved
            // to DataRow(string?[]? stringArrayData) constructor.
            // It also happens with [DataRow((object[]?)null)] which resolves
            // to the params object[] constructor
            // In this case, the argument is simply "null".
            if (constructorArgument.Kind == TypedConstantKind.Array && constructorArgument.IsNull)
            {
                continue;
            }

            if (constructorArgument.Type is null)
            {
                // That's an error scenario. The compiler will be complaining about something already.
                continue;
            }

            Type? argumentType = constructorArgument.Kind == TypedConstantKind.Array
                ? GetSystemType(((IArrayTypeSymbol)constructorArgument.Type).ElementType)
                : constructorArgument.Value?.GetType();

            if (argumentType is null)
            {
                continue;
            }

            ITypeSymbol parameterType = constructorArgument.Kind == TypedConstantKind.Array
                ? ((IArrayTypeSymbol)parameter.Type).ElementType
                : parameter.Type;

            if (parameterType.Kind != SymbolKind.TypeParameter)
            {
                continue;
            }

            if (parameterTypesSubstitutions.TryGetValue(parameterType, out (ITypeSymbol Symbol, Type SystemType) existingType))
            {
                if (argumentType.IsAssignableTo(existingType.SystemType))
                {
                    continue;
                }
                else if (existingType.SystemType.IsAssignableTo(argumentType))
                {
                    parameterTypesSubstitutions[parameterType] = (parameterType, argumentType);
                }
                else
                {
                    context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(GenericTypeArgumentConflictingTypesRule, parameterType.Name, existingType.Symbol.Name, constructorArgument.Type.Name));
                }
            }
            else
            {
                parameterTypesSubstitutions.Add(parameterType, (constructorArgument.Type, argumentType));
            }
        }

        foreach (ITypeParameterSymbol typeParameter in methodSymbol.TypeParameters)
        {
            if (!parameterTypesSubstitutions.ContainsKey(typeParameter))
            {
                context.ReportDiagnostic(dataRowSyntax.CreateDiagnostic(GenericTypeArgumentNotResolvedRule, typeParameter.Name));
            }
        }
    }

    private static ITypeSymbol GetParameterType(ImmutableArray<IParameterSymbol> parameters, int currentArgumentIndex, int argumentsCount)
    {
        if (currentArgumentIndex >= parameters.Length - 1)
        {
            IParameterSymbol lastParameter = parameters[^1];

            // When last parameter is params, we want to check that the extra arguments match the type of the array
            if (lastParameter.IsParams)
            {
                return ((IArrayTypeSymbol)lastParameter.Type).ElementType;
            }

            // When only parameter is array and we have more than one argument, we want to check the array type
            if (argumentsCount > 1 && parameters.Length == 1 && lastParameter.Type.Kind == SymbolKind.ArrayType)
            {
                return ((IArrayTypeSymbol)lastParameter.Type).ElementType;
            }
        }

        return parameters[currentArgumentIndex].Type;
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
