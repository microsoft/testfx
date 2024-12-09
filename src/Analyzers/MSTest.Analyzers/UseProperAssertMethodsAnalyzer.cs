// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using Analyzer.Utilities;
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

    internal const string ProperAssertMethodNameKey = nameof(ProperAssertMethodNameKey);

    /// <summary>
    /// Only the presence of this key in properties bag indicates that a cast is needed.
    /// The value of the key is always null.
    /// </summary>
    internal const string NeedsNullableBooleanCastKey = nameof(NeedsNullableBooleanCastKey);

    /// <summary>
    /// Key in the properties bag that has value one of CodeFixModeSimple, CodeFixModeAddArgument, or CodeFixModeRemoveArgument.
    /// </summary>
    internal const string CodeFixModeKey = nameof(CodeFixModeKey);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Replace the syntax node from the first additional locations with the node from second additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.IsTrue(x == null)</c>, it will become <c>Assert.IsNull(x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "IsNull".</para>
    /// <para>The first additional location will point to the "x == null" node.</para>
    /// <para>The second additional location will point to the "x" node.</para>
    /// </summary>
    internal const string CodeFixModeSimple = nameof(CodeFixModeSimple);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Replace the syntax node from the first additional locations with the node from second additional locations.</item>
    /// <item>Add new argument which is identical to the node from third additional locations.</item>
    /// </list>
    /// <para>Example: For <c>Assert.IsTrue(x == y)</c>, it will become <c>Assert.AreEqual(y, x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "AreEqual".</para>
    /// <para>The first additional location will point to the "x == y" node.</para>
    /// <para>The second additional location will point to the "y" node.</para>
    /// <para>The third additional location will point to the "x" node.</para>
    /// </summary>
    internal const string CodeFixModeAddArgument = nameof(CodeFixModeAddArgument);

    /// <summary>
    /// This mode means the codefix operation is as follows:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name. The identifier syntax is calculated by the codefix.</item>
    /// <item>Remove the argument which the second additional locations points to.</item>
    /// </list>
    /// <para>Example: For <c>Assert.AreEqual(false, x)</c>, it will become <c>Assert.IsFalse(x)</c>.</para>
    /// <para>The value for ProperAssertMethodNameKey is "IsFalse".</para>
    /// <para>The first additional location will point to the "false" node.</para>
    /// <para>The second additional location will point to the "x" node, in case a cast is needed.</para>
    /// </summary>
    /// <remarks>
    /// If <see cref="NeedsNullableBooleanCastKey"/> is present, then the produced code will be <c>Assert.IsFalse((bool?)x);</c>.
    /// </remarks>
    internal const string CodeFixModeRemoveArgument = nameof(CodeFixModeRemoveArgument);

    private static readonly LocalizableResourceString Title = new(nameof(Resources.UseProperAssertMethodsTitle), Resources.ResourceManager, typeof(Resources));
    private static readonly LocalizableResourceString MessageFormat = new(nameof(Resources.UseProperAssertMethodsMessageFormat), Resources.ResourceManager, typeof(Resources));

    internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
        DiagnosticIds.UseProperAssertMethodsRuleId,
        Title,
        MessageFormat,
        null,
        Category.Usage,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

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

    private static bool IsIsNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            return true;
        }

        expressionUnderTest = null;
        return false;
    }

    private static bool IsIsNotNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            return true;
        }

        expressionUnderTest = null;
        return false;
    }

    // TODO: Recognize 'null == something' (i.e, when null is the left operand)
    private static bool IsEqualsNullBinaryOperator(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, RightOperand: { } rightOperand } binaryOperation &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = binaryOperation.LeftOperand.Syntax;
            return true;
        }

        expressionUnderTest = null;
        return false;
    }

    // TODO: Recognize 'null != something' (i.e, when null is the left operand)
    private static bool IsNotEqualsNullBinaryOperator(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, RightOperand: { } rightOperand } binaryOperation &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = binaryOperation.LeftOperand.Syntax;
            return true;
        }

        expressionUnderTest = null;
        return false;
    }

    private static NullCheckStatus RecognizeNullCheck(IOperation operation, /*We cannot express this is not null when NullCheckStatis is not Unknown*/ out SyntaxNode? expressionUnderTest)
    {
        if (IsIsNullPattern(operation, out expressionUnderTest) || IsEqualsNullBinaryOperator(operation, out expressionUnderTest))
        {
            return NullCheckStatus.IsNull;
        }
        else if (IsIsNotNullPattern(operation, out expressionUnderTest) || IsNotEqualsNullBinaryOperator(operation, out expressionUnderTest))
        {
            return NullCheckStatus.IsNotNull;
        }

        return NullCheckStatus.Unknown;
    }

    private static EqualityCheckStatus RecognizeEqualityCheck(IOperation operation, out SyntaxNode? toBecomeExpected, out SyntaxNode? toBecomeActual)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation constantPattern1 } isPattern1)
        {
            toBecomeExpected = constantPattern1.Syntax;
            toBecomeActual = isPattern1.Value.Syntax;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } binaryOperation1)
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation1.RightOperand.Syntax;
            toBecomeActual = binaryOperation1.LeftOperand.Syntax;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation constantPattern2 } } isPattern2)
        {
            toBecomeExpected = constantPattern2.Syntax;
            toBecomeActual = isPattern2.Value.Syntax;
            return EqualityCheckStatus.NotEquals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } binaryOperation2)
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation2.RightOperand.Syntax;
            toBecomeActual = binaryOperation2.LeftOperand.Syntax;
            return EqualityCheckStatus.NotEquals;
        }

        toBecomeExpected = null;
        toBecomeActual = null;
        return EqualityCheckStatus.Unknown;
    }

    private static void AnalyzeIsTrueOrIsFalseInvocation(OperationAnalysisContext context, IOperation conditionArgument, bool isTrueInvocation)
    {
        RoslynDebug.Assert(context.Operation is IInvocationOperation, "Expected IInvocationOperation.");

        NullCheckStatus nullCheckStatus = RecognizeNullCheck(conditionArgument, out SyntaxNode? expressionUnderTest);
        if (nullCheckStatus != NullCheckStatus.Unknown)
        {
            RoslynDebug.Assert(expressionUnderTest is not null, $"Unexpected null for '{nameof(expressionUnderTest)}'.");
            RoslynDebug.Assert(nullCheckStatus is NullCheckStatus.IsNull or NullCheckStatus.IsNotNull, "Unexpected NullCheckStatus value.");
            bool shouldUseIsNull = isTrueInvocation
                ? nullCheckStatus == NullCheckStatus.IsNull
                : nullCheckStatus == NullCheckStatus.IsNotNull;

            // Here, the codefix will want to switch something like Assert.IsTrue(x == null) with Assert.IsNull(x)
            // This is the "simple" mode.

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            string properAssertMethod = shouldUseIsNull ? "IsNull" : "IsNotNull";
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), expressionUnderTest.GetLocation()),
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add(ProperAssertMethodNameKey, properAssertMethod)
                    .Add(CodeFixModeKey, CodeFixModeSimple),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        EqualityCheckStatus equalityCheckStatus = RecognizeEqualityCheck(conditionArgument, out SyntaxNode? toBecomeExpected, out SyntaxNode? toBecomeActual);
        if (equalityCheckStatus != EqualityCheckStatus.Unknown)
        {
            RoslynDebug.Assert(toBecomeExpected is not null, $"Unexpected null for '{nameof(toBecomeExpected)}'.");
            RoslynDebug.Assert(toBecomeActual is not null, $"Unexpected null for '{nameof(toBecomeActual)}'.");
            RoslynDebug.Assert(equalityCheckStatus is EqualityCheckStatus.Equals or EqualityCheckStatus.NotEquals, "Unexpected EqualityCheckStatus value.");
            bool shouldUseAreEqual = isTrueInvocation
                ? equalityCheckStatus == EqualityCheckStatus.Equals
                : equalityCheckStatus == EqualityCheckStatus.NotEquals;

            // Here, the codefix will want to switch something like Assert.IsTrue(x == y) with Assert.AreEqual(x, y)
            // This is the "add argument" mode.

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            string properAssertMethod = shouldUseAreEqual ? "AreEqual" : "AreNotEqual";
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), toBecomeExpected.GetLocation(), toBecomeActual.GetLocation()),
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add(ProperAssertMethodNameKey, properAssertMethod)
                    .Add(CodeFixModeKey, CodeFixModeAddArgument),
                properAssertMethod,
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

            // Here, the codefix will want to switch something like Assert.AreEqual(true, x) with Assert.IsTrue(x)
            // This is the "remove argument" mode.

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            string properAssertMethod = shouldUseIsTrue ? "IsTrue" : "IsFalse";

            bool codeFixShouldAddCast = TryGetSecondArgumentValue((IInvocationOperation)context.Operation, out IOperation? actualArgumentValue) &&
                actualArgumentValue.Type is { } actualType &&
                actualType.SpecialType != SpecialType.System_Boolean &&
                !actualType.IsNullableOfBoolean();

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ProperAssertMethodNameKey, properAssertMethod)
                .Add(CodeFixModeKey, CodeFixModeRemoveArgument);

            if (codeFixShouldAddCast)
            {
                properties = properties.Add(NeedsNullableBooleanCastKey, null);
            }

            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(expectedArgument.Syntax.GetLocation(), actualArgumentValue?.Syntax.GetLocation() ?? Location.None),
                properties: properties,
                properAssertMethod,
                isAreEqualInvocation ? "AreEqual" : "AreNotEqual"));
        }
        else if (expectedArgument is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            bool shouldUseIsNull = isAreEqualInvocation;

            // Here, the codefix will want to switch something like Assert.AreEqual(null, x) with Assert.IsNull(x)
            // This is the "remove argument" mode.

            // The message is: Use 'Assert.{0}' instead of 'Assert.{1}'.
            string properAssertMethod = shouldUseIsNull ? "IsNull" : "IsNotNull";
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(expectedArgument.Syntax.GetLocation()),
                properties: ImmutableDictionary<string, string?>.Empty
                    .Add(ProperAssertMethodNameKey, properAssertMethod)
                    .Add(CodeFixModeKey, CodeFixModeRemoveArgument), properAssertMethod,
                isAreEqualInvocation ? "AreEqual" : "AreNotEqual"));
        }
    }

    private static bool TryGetFirstArgumentValue(IInvocationOperation operation, [NotNullWhen(true)] out IOperation? argumentValue)
        => TryGetArgumentValueForParameterOrdinal(operation, 0, out argumentValue);

    private static bool TryGetSecondArgumentValue(IInvocationOperation operation, [NotNullWhen(true)] out IOperation? argumentValue)
        => TryGetArgumentValueForParameterOrdinal(operation, 1, out argumentValue);

    private static bool TryGetArgumentValueForParameterOrdinal(IInvocationOperation operation, int ordinal, [NotNullWhen(true)] out IOperation? argumentValue)
    {
        argumentValue = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == ordinal)?.Value?.WalkDownConversion();
        return argumentValue is not null;
    }
}
