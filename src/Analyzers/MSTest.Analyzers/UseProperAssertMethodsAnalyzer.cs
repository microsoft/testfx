// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;
using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

/// <summary>
/// MSTEST0037: Use proper 'Assert' methods.
/// </summary>
/// <remarks>
/// The analyzer captures the following cases:
/// <list type="bullet">
/// <item>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=|is|is not] null)</code>
/// </item>
/// <item>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=] y)</code>
/// </item>
/// <item>
/// <code>Assert.AreEqual([true|false], x)</code>
/// </item>
/// <item>
/// <code>Assert.[AreEqual|AreNotEqual](null, x)</code>
/// </item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
internal sealed class UseProperAssertMethodsAnalyzer : DiagnosticAnalyzer
{
    private enum NullCheckStatus
    {
        Unknown,
        IsNull,
        IsNotNull,
    }

    private enum EqualityCheckStatus
    {
        Unknown,
        Equals,
        NotEquals,
    }

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseProperAssertMethodsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseProperAssertMethodsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseProperAssertMethodsRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(context =>
        {
            if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftVisualStudioTestToolsUnitTestingAssert, out INamedTypeSymbol? assertTypeSymbol))
            {
                return;
            }

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, assertTypeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol assertTypeSymbol)
    {
        var operation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = operation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, assertTypeSymbol))
        {
            return;
        }

        if (!TryGetFirstArgumentValue(operation, out IOperation? firstArgument))
        {
            return;
        }

        switch (targetMethod.Name)
        {
            case "IsTrue":
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: true);
                break;

            case "IsFalse":
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: false);
                break;

            case "AreEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: true);
                break;

            case "AreNotEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: false);
                break;
        }
    }

    private static bool IsIsNullPattern(IOperation operation)
        => operation is IIsPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };

    private static bool IsIsNotNullPattern(IOperation operation)
        => operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } } &&
        constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };

    // TODO: Recognize 'null == something' (i.e, when null is the left operand)
    private static bool IsEqualsNullBinaryOperator(IOperation operation)
        => operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, RightOperand: { } rightOperand } &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };

    // TODO: Recognize 'null != something' (i.e, when null is the left operand)
    private static bool IsNotEqualsNullBinaryOperator(IOperation operation)
        => operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, RightOperand: { } rightOperand } &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } };

    private static NullCheckStatus RecognizeNullCheck(IOperation operation)
    {
        if (IsIsNullPattern(operation) || IsEqualsNullBinaryOperator(operation))
        {
            return NullCheckStatus.IsNull;
        }
        else if (IsIsNotNullPattern(operation) || IsNotEqualsNullBinaryOperator(operation))
        {
            return NullCheckStatus.IsNotNull;
        }

        return NullCheckStatus.Unknown;
    }

    private static EqualityCheckStatus RecognizeEqualityCheck(IOperation operation)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation } or
            IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals })
        {
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation } } or
            IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals })
        {
            return EqualityCheckStatus.NotEquals;
        }

        return EqualityCheckStatus.Unknown;
    }

    private static void AnalyzeIsTrueOrIsFalseInvocation(OperationAnalysisContext context, IOperation conditionArgument, bool isTrueInvocation)
    {
        NullCheckStatus nullCheckStatus = RecognizeNullCheck(conditionArgument);
        if (nullCheckStatus != NullCheckStatus.Unknown)
        {
            Debug.Assert(nullCheckStatus is NullCheckStatus.IsNull or NullCheckStatus.IsNotNull, "Unexpected NullCheckStatus value.");
            bool shouldUseIsNull = isTrueInvocation
                ? nullCheckStatus == NullCheckStatus.IsNull
                : nullCheckStatus == NullCheckStatus.IsNotNull;

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                shouldUseIsNull ? "IsNull" : "IsNotNull",
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        EqualityCheckStatus equalityCheckStatus = RecognizeEqualityCheck(conditionArgument);
        if (equalityCheckStatus != EqualityCheckStatus.Unknown)
        {
            Debug.Assert(equalityCheckStatus is EqualityCheckStatus.Equals or EqualityCheckStatus.NotEquals, "Unexpected EqualityCheckStatus value.");
            bool shouldUseAreEqual = isTrueInvocation
                ? equalityCheckStatus == EqualityCheckStatus.Equals
                : equalityCheckStatus == EqualityCheckStatus.NotEquals;

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                shouldUseAreEqual ? "AreEqual" : "AreNotEqual",
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }
    }

    private static void AnalyzeAreEqualOrAreNotEqualInvocation(OperationAnalysisContext context, IOperation expectedArgument, bool isAreEqualInvocation)
    {
        // Don't flag a warning for Assert.AreNotEqual([true|false], x).
        // This is not the same as Assert.IsFalse(x).
        if (isAreEqualInvocation && expectedArgument is ILiteralOperation { ConstantValue: { HasValue: true, Value: bool expectedLiteralBoolean } })
        {
            bool shouldUseIsTrue = expectedLiteralBoolean;

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                shouldUseIsTrue ? "IsTrue" : "IsFalse",
                isAreEqualInvocation ? "AreEqual" : "AreNotEqual"));
        }
        else if (expectedArgument is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            bool shouldUseIsNull = isAreEqualInvocation;

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                shouldUseIsNull ? "IsNull" : "IsNotNull",
                isAreEqualInvocation ? "AreEqual" : "AreNotEqual"));
        }
    }

    private static bool TryGetFirstArgumentValue(IInvocationOperation operation, [NotNullWhen(true)] out IOperation? argumentValue)
        => TryGetArgumentValueForParameterOrdinal(operation, 0, out argumentValue);

    private static bool TryGetArgumentValueForParameterOrdinal(IInvocationOperation operation, int ordinal, [NotNullWhen(true)] out IOperation? argumentValue)
    {
        argumentValue = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == ordinal)?.Value?.WalkDownConversion();
        return argumentValue is not null;
    }
}
