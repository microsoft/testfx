// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0032: <inheritdoc cref="Resources.ReviewAlwaysTrueAssertConditionAnalyzerTitle"/>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ReviewAlwaysTrueAssertConditionAnalyzer : DiagnosticAnalyzer
{
    private static readonly LocalizableResourceString Title = new(nameof(Resources.ReviewAlwaysTrueAssertConditionAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.ReviewAlwaysTrueAssertConditionAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString Description = new(nameof(Resources.ReviewAlwaysTrueAssertConditionAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.ReviewAlwaysTrueAssertConditionAnalyzerRuleId,
        Title,
        MessageFormat,
        Description,
        Category.Design,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterCompilationStartAction(context =>
        {
            Compilation compilation = context.Compilation;
            INamedTypeSymbol? assertSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert);
            if (assertSymbol is not null)
            {
                context.RegisterOperationAction(context => AnalyzeOperation(context, assertSymbol), OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, INamedTypeSymbol assertSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        if (assertSymbol.Equals(operation.TargetMethod.ContainingType, SymbolEqualityComparer.Default) &&
            IsAlwaysTrue(operation, context.Compilation.Assembly))
        {
            context.ReportDiagnostic(operation.CreateDiagnostic(Rule));
        }
    }

    private static bool IsAlwaysTrue(IInvocationOperation operation, IAssemblySymbol currentAssembly)
        => operation.TargetMethod.Name switch
        {
            "IsTrue" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: true } },
            "IsFalse" => AssertConditionAnalyzerHelper.GetConditionArgument(operation) is { ConstantValue: { HasValue: true, Value: false } },
            "AreEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && !IsEnumUnderlyingValueContractAssertion(operation)
                && !IsReferencedConstantContractAssertion(operation, currentAssembly)
                && (AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.ExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.Equal
                    || AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActualWithBuiltInEquality(operation, AssertConditionAnalyzerHelper.ExpectedParameterName)),
            "AreNotEqual" => !AssertConditionAnalyzerHelper.HasNonDefaultEqualityComparerArgument(operation)
                && AssertConditionAnalyzerHelper.GetEqualityStatus(operation, AssertConditionAnalyzerHelper.NotExpectedParameterName) == AssertConditionAnalyzerHelper.EqualityStatus.NotEqual,
            "AreSame" => AssertConditionAnalyzerHelper.HasIdenticalExpectedAndActual(operation, AssertConditionAnalyzerHelper.ExpectedParameterName),
            "IsNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { ConstantValue: { HasValue: true, Value: null } },
            "IsNotNull" => AssertConditionAnalyzerHelper.GetValueArgument(operation) is { } valueArgumentOperation && AssertConditionAnalyzerHelper.IsNotNullableType(valueArgumentOperation),
            _ => false,
        };

    // Detects assertions comparing a literal with a const field from another assembly, in either argument order,
    // such as Assert.AreEqual(12, Protocol.HeaderVersion), which can pin the dependency's contract.
    private static bool IsReferencedConstantContractAssertion(IInvocationOperation operation, IAssemblySymbol currentAssembly)
    {
        IOperation? expectedArgument = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == AssertConditionAnalyzerHelper.ExpectedParameterName)?.Value;
        IOperation? actualArgument = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == AssertConditionAnalyzerHelper.ActualParameterName)?.Value;

        return expectedArgument is not null
            && actualArgument is not null
            && ((IsContractLiteral(expectedArgument) && IsReferencedConstant(actualArgument, currentAssembly))
                || (IsReferencedConstant(expectedArgument, currentAssembly) && IsContractLiteral(actualArgument)));
    }

    private static bool IsContractLiteral(IOperation operation)
        => WalkDownImplicitBuiltInConversionsAndParentheses(operation) switch
        {
            ILiteralOperation => true,
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Plus or UnaryOperatorKind.Minus, OperatorMethod: null, Operand: { } operand } => IsContractLiteral(operand),
            _ => false,
        };

    private static bool IsReferencedConstant(IOperation operation, IAssemblySymbol currentAssembly)
        // A literal anchors a dependency's contract independently of its current value. Constants compiled
        // into the test assembly (including linked source) intentionally remain diagnostic.
        => WalkDownImplicitBuiltInConversionsAndParentheses(operation) is IFieldReferenceOperation { Field: { IsConst: true, Type.TypeKind: not TypeKind.Enum } field }
            && !SymbolEqualityComparer.Default.Equals(field.ContainingAssembly, currentAssembly);

    private static IOperation WalkDownImplicitBuiltInConversionsAndParentheses(IOperation operation)
        => operation switch
        {
            IConversionOperation { IsImplicit: true, Conversion.IsUserDefined: false } conversion => WalkDownImplicitBuiltInConversionsAndParentheses(conversion.Operand),
            IParenthesizedOperation parenthesizedOperation => WalkDownImplicitBuiltInConversionsAndParentheses(parenthesizedOperation.Operand),
            _ => operation,
        };

    private static bool IsEnumUnderlyingValueContractAssertion(IInvocationOperation operation)
    {
        IOperation? expectedArgument = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == AssertConditionAnalyzerHelper.ExpectedParameterName)?.Value;
        IOperation? actualArgument = operation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == AssertConditionAnalyzerHelper.ActualParameterName)?.Value;

        return expectedArgument is not null
            && actualArgument is not null
            && ((IsNumericLiteral(expectedArgument) && IsEnumMemberConvertedToUnderlyingType(actualArgument))
                || (IsEnumMemberConvertedToUnderlyingType(expectedArgument) && IsNumericLiteral(actualArgument)));
    }

    private static bool IsNumericLiteral(IOperation operation)
        => WalkDownImplicitConversionsAndParentheses(operation) switch
        {
            ILiteralOperation { Type.SpecialType: var specialType } => IsIntegralNumericType(specialType),
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Plus or UnaryOperatorKind.Minus, Operand: { } operand } => IsNumericLiteral(operand),
            _ => false,
        };

    private static bool IsIntegralNumericType(SpecialType specialType)
        => specialType is SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;

    private static bool IsEnumMemberConvertedToUnderlyingType(IOperation operation)
    {
        operation = WalkDownImplicitConversionsAndParentheses(operation);
        if (operation is not IConversionOperation
            {
                IsImplicit: false,
                Type: { } convertedType,
                Operand: { } operand,
            })
        {
            return false;
        }

        operand = WalkDownImplicitConversionsAndParentheses(operand);
        return GetEnumUnderlyingType(operand) is { } underlyingType
            && SymbolEqualityComparer.Default.Equals(convertedType, underlyingType);
    }

    private static ITypeSymbol? GetEnumUnderlyingType(IOperation operation)
        => operation is IFieldReferenceOperation { Field: { HasConstantValue: true, ContainingType: { TypeKind: TypeKind.Enum } enumType } }
            ? enumType.EnumUnderlyingType
            : null;

    private static IOperation WalkDownImplicitConversionsAndParentheses(IOperation operation)
        => operation switch
        {
            IConversionOperation { IsImplicit: true } conversion => WalkDownImplicitConversionsAndParentheses(conversion.Operand),
            IParenthesizedOperation parenthesizedOperation => WalkDownImplicitConversionsAndParentheses(parenthesizedOperation.Operand),
            _ => operation,
        };
}
