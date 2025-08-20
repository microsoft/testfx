// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

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
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=|is|is not] null)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [==|!=] y)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreEqual([true|false], x)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[AreEqual|AreNotEqual](null, x)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myString.[StartsWith|EndsWith|Contains]("..."))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myString.[StartsWith|EndsWith|Contains]("..."))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myCollection.Contains(...))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsFalse(myCollection.Contains(...))</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.[IsTrue|IsFalse](x [&gt;|&gt;=|&lt;|&lt;=] y)</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.AreEqual([0|X], myCollection.[Count|Length])</code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <code>Assert.IsTrue(myCollection.[Count|Length] [&gt;|!=|==] 0)</code>
/// </description>
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

    private enum StringMethodCheckStatus
    {
        Unknown,
        StartsWith,
        EndsWith,
        Contains,
    }

    private enum ComparisonCheckStatus
    {
        Unknown,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
    }

    private enum CollectionCheckStatus
    {
        Unknown,
        Contains,
    }

    private enum CountCheckStatus
    {
        Unknown,
        IsEmpty,
        HasCount,
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

    /// <summary>
    /// This mode means the codefix operation is as follows for collection count checks:
    /// <list type="number">
    /// <item>Find the right assert method name from the properties bag using <see cref="ProperAssertMethodNameKey"/>.</item>
    /// <item>Replace the identifier syntax for the invocation with the right assert method name.</item>
    /// <item>Transform arguments based on the count check pattern.</item>
    /// </list>
    /// <para>Example: For <c>Assert.AreEqual(0, list.Count)</c>, it will become <c>Assert.IsEmpty(list)</c>.</para>
    /// <para>Example: For <c>Assert.AreEqual(3, list.Count)</c>, it will become <c>Assert.HasCount(3, list)</c>.</para>
    /// </summary>
    internal const string CodeFixModeCollectionCount = nameof(CodeFixModeCollectionCount);

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

            INamedTypeSymbol objectTypeSymbol = context.Compilation.GetSpecialType(SpecialType.System_Object);

            context.RegisterOperationAction(context => AnalyzeInvocationOperation(context, assertTypeSymbol, objectTypeSymbol), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocationOperation(OperationAnalysisContext context, INamedTypeSymbol assertTypeSymbol, INamedTypeSymbol objectTypeSymbol)
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
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: true, objectTypeSymbol);
                break;

            case "IsFalse":
                AnalyzeIsTrueOrIsFalseInvocation(context, firstArgument, isTrueInvocation: false, objectTypeSymbol);
                break;

            case "AreEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: true, objectTypeSymbol);
                break;

            case "AreNotEqual":
                AnalyzeAreEqualOrAreNotEqualInvocation(context, firstArgument, isAreEqualInvocation: false, objectTypeSymbol);
                break;
        }
    }

    private static bool IsIsNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            typeOfExpressionUnderTest = isPatternOperation.Value.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static bool IsIsNotNullPattern(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation { Value: { } constantPatternValue } } } isPatternOperation &&
            constantPatternValue.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = isPatternOperation.Value.Syntax;
            typeOfExpressionUnderTest = isPatternOperation.Value.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    // TODO: Recognize 'null == something' (i.e, when null is the left operand)
    private static bool IsEqualsNullBinaryOperator(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, RightOperand: { } rightOperand } binaryOperation &&
            binaryOperation.OperatorMethod is not { MethodKind: MethodKind.UserDefinedOperator } &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = binaryOperation.LeftOperand.Syntax;
            typeOfExpressionUnderTest = binaryOperation.LeftOperand.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    // TODO: Recognize 'null != something' (i.e, when null is the left operand)
    private static bool IsNotEqualsNullBinaryOperator(IOperation operation, [NotNullWhen(true)] out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, RightOperand: { } rightOperand } binaryOperation &&
            binaryOperation.OperatorMethod is not { MethodKind: MethodKind.UserDefinedOperator } &&
            rightOperand.WalkDownConversion() is ILiteralOperation { ConstantValue: { HasValue: true, Value: null } })
        {
            expressionUnderTest = binaryOperation.LeftOperand.Syntax;
            typeOfExpressionUnderTest = binaryOperation.LeftOperand.WalkDownConversion().Type;
            return true;
        }

        expressionUnderTest = null;
        typeOfExpressionUnderTest = null;
        return false;
    }

    private static NullCheckStatus RecognizeNullCheck(
        IOperation operation,
        // Note that expressionUnderTest is guaranteed to be non-null when the method returns a value other than NullCheckStatus.Unknown.
        // Given the current nullability attributes, there is no way to express this.
        out SyntaxNode? expressionUnderTest,
        out ITypeSymbol? typeOfExpressionUnderTest)
    {
        if (IsIsNullPattern(operation, out expressionUnderTest, out typeOfExpressionUnderTest) ||
            IsEqualsNullBinaryOperator(operation, out expressionUnderTest, out typeOfExpressionUnderTest))
        {
            return NullCheckStatus.IsNull;
        }
        else if (IsIsNotNullPattern(operation, out expressionUnderTest, out typeOfExpressionUnderTest) ||
            IsNotEqualsNullBinaryOperator(operation, out expressionUnderTest, out typeOfExpressionUnderTest))
        {
            return NullCheckStatus.IsNotNull;
        }

        return NullCheckStatus.Unknown;
    }

    private static EqualityCheckStatus RecognizeEqualityCheck(
        IOperation operation,
        out SyntaxNode? toBecomeExpected,
        out SyntaxNode? toBecomeActual,
        out ITypeSymbol? leftType,
        out ITypeSymbol? rightType)
    {
        if (operation is IIsPatternOperation { Pattern: IConstantPatternOperation constantPattern1 } isPattern1)
        {
            toBecomeExpected = constantPattern1.Syntax;
            toBecomeActual = isPattern1.Value.Syntax;
            leftType = constantPattern1.WalkDownConversion().Type;
            rightType = isPattern1.Value.WalkDownConversion().Type;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } binaryOperation1 &&
            binaryOperation1.OperatorMethod is not { MethodKind: MethodKind.UserDefinedOperator })
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation1.RightOperand.Syntax;
            toBecomeActual = binaryOperation1.LeftOperand.Syntax;
            leftType = binaryOperation1.RightOperand.WalkDownConversion().Type;
            rightType = binaryOperation1.LeftOperand.WalkDownConversion().Type;
            return EqualityCheckStatus.Equals;
        }
        else if (operation is IIsPatternOperation { Pattern: INegatedPatternOperation { Pattern: IConstantPatternOperation constantPattern2 } } isPattern2)
        {
            toBecomeExpected = constantPattern2.Syntax;
            toBecomeActual = isPattern2.Value.Syntax;
            leftType = constantPattern2.WalkDownConversion().Type;
            rightType = isPattern2.Value.WalkDownConversion().Type;
            return EqualityCheckStatus.NotEquals;
        }
        else if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals } binaryOperation2 &&
            binaryOperation2.OperatorMethod is not { MethodKind: MethodKind.UserDefinedOperator })
        {
            // This is quite arbitrary. We can do extra checks to see which one (if any) looks like a "constant" and make it the expected.
            toBecomeExpected = binaryOperation2.RightOperand.Syntax;
            toBecomeActual = binaryOperation2.LeftOperand.Syntax;
            leftType = binaryOperation2.RightOperand.WalkDownConversion().Type;
            rightType = binaryOperation2.LeftOperand.WalkDownConversion().Type;
            return EqualityCheckStatus.NotEquals;
        }

        toBecomeExpected = null;
        toBecomeActual = null;
        leftType = null;
        rightType = null;
        return EqualityCheckStatus.Unknown;
    }

    private static bool CanUseTypeAsObject(Compilation compilation, ITypeSymbol? type)
        => type is null ||
            compilation.ClassifyCommonConversion(type, compilation.GetSpecialType(SpecialType.System_Object)).Exists;

    private static StringMethodCheckStatus RecognizeStringMethodCheck(
        IOperation operation,
        out SyntaxNode? stringExpression,
        out SyntaxNode? substringExpression)
    {
        if (operation is IInvocationOperation invocation &&
            invocation.TargetMethod.ContainingType?.SpecialType == SpecialType.System_String &&
            invocation.Arguments.Length == 1)
        {
            string methodName = invocation.TargetMethod.Name;
            if (methodName is "StartsWith" or "EndsWith" or "Contains")
            {
                stringExpression = invocation.Instance?.Syntax;
                substringExpression = invocation.Arguments[0].Value.Syntax;

                return methodName switch
                {
                    "StartsWith" => StringMethodCheckStatus.StartsWith,
                    "EndsWith" => StringMethodCheckStatus.EndsWith,
                    "Contains" => StringMethodCheckStatus.Contains,
                    _ => StringMethodCheckStatus.Unknown,
                };
            }
        }

        stringExpression = null;
        substringExpression = null;
        return StringMethodCheckStatus.Unknown;
    }

    private static CollectionCheckStatus RecognizeCollectionMethodCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? itemExpression)
    {
        if (operation is IInvocationOperation invocation)
        {
            string methodName = invocation.TargetMethod.Name;

            // Check for Collection.Contains(item)
            if (methodName == "Contains" && invocation.Arguments.Length == 1)
            {
                if (IsBCLCollectionType(invocation.TargetMethod.ContainingType, objectTypeSymbol))
                {
                    collectionExpression = invocation.Instance?.Syntax;
                    itemExpression = invocation.Arguments[0].Value.Syntax;
                    return CollectionCheckStatus.Contains;
                }
            }
        }

        collectionExpression = null;
        itemExpression = null;
        return CollectionCheckStatus.Unknown;
    }

    private static bool IsBCLCollectionType(ITypeSymbol type, INamedTypeSymbol objectTypeSymbol)
        // Check if the type implements IEnumerable<T> (but is not string)
        // Note: Assert.Contains/IsEmpty/HasCount for collections accept IEnumerable<T>, but not IEnumerable.
        => type.SpecialType != SpecialType.System_String && type.AllInterfaces.Any(i =>
            i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T) &&
            // object is coming from BCL and it's expected to always have a public key.
            type.ContainingAssembly.Identity.HasPublicKey == objectTypeSymbol.ContainingAssembly.Identity.HasPublicKey &&
            type.ContainingAssembly.Identity.PublicKey.SequenceEqual(objectTypeSymbol.ContainingAssembly.Identity.PublicKey);

    private static ComparisonCheckStatus RecognizeComparisonCheck(
        IOperation operation,
        out SyntaxNode? leftExpression,
        out SyntaxNode? rightExpression)
    {
        if (operation is IBinaryOperation binaryOperation &&
            binaryOperation.OperatorMethod is not { MethodKind: MethodKind.UserDefinedOperator })
        {
            leftExpression = binaryOperation.LeftOperand.Syntax;
            rightExpression = binaryOperation.RightOperand.Syntax;

            return binaryOperation.OperatorKind switch
            {
                BinaryOperatorKind.GreaterThan => ComparisonCheckStatus.GreaterThan,
                BinaryOperatorKind.GreaterThanOrEqual => ComparisonCheckStatus.GreaterThanOrEqual,
                BinaryOperatorKind.LessThan => ComparisonCheckStatus.LessThan,
                BinaryOperatorKind.LessThanOrEqual => ComparisonCheckStatus.LessThanOrEqual,
                _ => ComparisonCheckStatus.Unknown,
            };
        }

        leftExpression = null;
        rightExpression = null;
        return ComparisonCheckStatus.Unknown;
    }

    private static void AnalyzeIsTrueOrIsFalseInvocation(OperationAnalysisContext context, IOperation conditionArgument, bool isTrueInvocation, INamedTypeSymbol objectTypeSymbol)
    {
        RoslynDebug.Assert(context.Operation is IInvocationOperation, "Expected IInvocationOperation.");

        NullCheckStatus nullCheckStatus = RecognizeNullCheck(conditionArgument, out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest);

        // In this code path, we will be suggesting the use of IsNull/IsNotNull.
        // These assert methods only have an "object" overload.
        // For example, pointer types cannot be converted to object.
        if (nullCheckStatus != NullCheckStatus.Unknown &&
            CanUseTypeAsObject(context.Compilation, typeOfExpressionUnderTest))
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

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeSimple);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), expressionUnderTest.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        // Check for string method patterns: myString.StartsWith/EndsWith/Contains(...)
        StringMethodCheckStatus stringMethodStatus = RecognizeStringMethodCheck(conditionArgument, out SyntaxNode? stringExpr, out SyntaxNode? substringExpr);
        if (stringMethodStatus != StringMethodCheckStatus.Unknown)
        {
            // Handle both IsTrue and IsFalse cases with string methods
            string properAssertMethod = stringMethodStatus switch
            {
                StringMethodCheckStatus.StartsWith => isTrueInvocation ? "StartsWith" : "DoesNotStartWith",
                StringMethodCheckStatus.EndsWith => isTrueInvocation ? "EndsWith" : "DoesNotEndWith",
                StringMethodCheckStatus.Contains => isTrueInvocation ? "Contains" : "DoesNotContain",
                _ => throw new InvalidOperationException("Unexpected StringMethodCheckStatus value."),
            };

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeAddArgument);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), substringExpr!.GetLocation(), stringExpr!.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        // Check for collection method patterns: myCollection.Contains(...)
        CollectionCheckStatus collectionMethodStatus = RecognizeCollectionMethodCheck(conditionArgument, objectTypeSymbol, out SyntaxNode? collectionExpr, out SyntaxNode? itemExpr);
        if (collectionMethodStatus != CollectionCheckStatus.Unknown)
        {
            if (collectionMethodStatus == CollectionCheckStatus.Contains)
            {
                string properAssertMethod = isTrueInvocation ? "Contains" : "DoesNotContain";

                ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(ProperAssertMethodNameKey, properAssertMethod);
                properties.Add(CodeFixModeKey, CodeFixModeAddArgument);
                context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                    Rule,
                    additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), itemExpr!.GetLocation(), collectionExpr!.GetLocation()),
                    properties: properties.ToImmutable(),
                    properAssertMethod,
                    isTrueInvocation ? "IsTrue" : "IsFalse"));
                return;
            }
        }

        // Check for collection emptiness patterns: myCollection.Count > 0, myCollection.Count != 0, or myCollection.Count == 0
        CountCheckStatus countStatus = RecognizeCountCheck(conditionArgument, objectTypeSymbol, out SyntaxNode? collectionEmptinessExpr);
        if (countStatus != CountCheckStatus.Unknown)
        {
            string properAssertMethod = countStatus switch
            {
                CountCheckStatus.IsEmpty => isTrueInvocation ? "IsEmpty" : "IsNotEmpty",
                CountCheckStatus.HasCount => isTrueInvocation ? "IsNotEmpty" : "IsEmpty",
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeSimple);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), collectionEmptinessExpr!.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        // Check for comparison patterns: a > b, a >= b, a < b, a <= b
        ComparisonCheckStatus comparisonStatus = RecognizeComparisonCheck(conditionArgument, out SyntaxNode? leftExpr, out SyntaxNode? rightExpr);
        if (comparisonStatus != ComparisonCheckStatus.Unknown)
        {
            string properAssertMethod = (isTrueInvocation, comparisonStatus) switch
            {
                (true, ComparisonCheckStatus.GreaterThan) => "IsGreaterThan",
                (true, ComparisonCheckStatus.GreaterThanOrEqual) => "IsGreaterThanOrEqualTo",
                (true, ComparisonCheckStatus.LessThan) => "IsLessThan",
                (true, ComparisonCheckStatus.LessThanOrEqual) => "IsLessThanOrEqualTo",
                (false, ComparisonCheckStatus.GreaterThan) => "IsLessThanOrEqualTo",
                (false, ComparisonCheckStatus.GreaterThanOrEqual) => "IsLessThan",
                (false, ComparisonCheckStatus.LessThan) => "IsGreaterThanOrEqualTo",
                (false, ComparisonCheckStatus.LessThanOrEqual) => "IsGreaterThan",
                _ => throw new InvalidOperationException("Unexpected ComparisonCheckStatus value."),
            };

            // For Assert.IsGreaterThan, IsLessThan etc., the method signature is (lowerBound, value) or (upperBound, value)
            // So for a > b -> Assert.IsGreaterThan(b, a) where b is the lower bound and a is the value
            // For a < b -> Assert.IsLessThan(b, a) where b is the upper bound and a is the value
            SyntaxNode? firstArg, secondArg;
            switch ((isTrueInvocation, comparisonStatus))
            {
                // a > b -> IsGreaterThan(b, a)
                case (true, ComparisonCheckStatus.GreaterThan):
                // a >= b -> IsGreaterThanOrEqualTo(b, a)
                case (true, ComparisonCheckStatus.GreaterThanOrEqual):
                // !(a < b) -> IsGreaterThanOrEqualTo(b, a)
                case (false, ComparisonCheckStatus.LessThan):
                // !(a <= b) -> IsGreaterThan(b, a)
                case (false, ComparisonCheckStatus.LessThanOrEqual):
                    firstArg = rightExpr;  // b becomes first arg (lower bound)
                    secondArg = leftExpr;  // a becomes second arg (value)
                    break;
                // a < b -> IsLessThan(b, a)
                case (true, ComparisonCheckStatus.LessThan):
                // a <= b -> IsLessThanOrEqualTo(b, a)
                case (true, ComparisonCheckStatus.LessThanOrEqual):
                // !(a > b) -> IsLessThanOrEqualTo(b, a)
                case (false, ComparisonCheckStatus.GreaterThan):
                // !(a >= b) -> IsLessThan(b, a)
                case (false, ComparisonCheckStatus.GreaterThanOrEqual):
                    firstArg = rightExpr;  // b becomes first arg (upper bound)
                    secondArg = leftExpr;  // a becomes second arg (value)
                    break;

                default:
                    throw new InvalidOperationException("Unexpected comparison case.");
            }

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeAddArgument);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), firstArg!.GetLocation(), secondArg!.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
            return;
        }

        EqualityCheckStatus equalityCheckStatus = RecognizeEqualityCheck(conditionArgument, out SyntaxNode? toBecomeExpected, out SyntaxNode? toBecomeActual, out ITypeSymbol? leftType, out ITypeSymbol? rightType);
        if (equalityCheckStatus != EqualityCheckStatus.Unknown &&
            CanUseTypeAsObject(context.Compilation, leftType) &&
            CanUseTypeAsObject(context.Compilation, rightType))
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
            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeAddArgument);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), toBecomeExpected.GetLocation(), toBecomeActual.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isTrueInvocation ? "IsTrue" : "IsFalse"));
        }
    }

    private static void AnalyzeAreEqualOrAreNotEqualInvocation(OperationAnalysisContext context, IOperation expectedArgument, bool isAreEqualInvocation, INamedTypeSymbol objectTypeSymbol)
    {
        // Check for collection count patterns: collection.Count/Length == 0 or collection.Count/Length == X
        if (isAreEqualInvocation)
        {
            if (TryGetSecondArgumentValue((IInvocationOperation)context.Operation, out IOperation? actualArgumentValue))
            {
                // Check if we're comparing a count/length property
                CountCheckStatus countStatus = RecognizeCountCheck(
                    expectedArgument,
                    actualArgumentValue,
                    objectTypeSymbol,
                    out SyntaxNode? collectionExpr,
                    out _,
                    out _);

                if (countStatus != CountCheckStatus.Unknown)
                {
                    string properAssertMethod = countStatus == CountCheckStatus.IsEmpty ? "IsEmpty" : "HasCount";

                    ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(ProperAssertMethodNameKey, properAssertMethod);

                    if (countStatus == CountCheckStatus.IsEmpty)
                    {
                        // Assert.IsEmpty(collection)
                        properties.Add(CodeFixModeKey, CodeFixModeCollectionCount);
                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                            Rule,
                            additionalLocations: ImmutableArray.Create(
                                expectedArgument.Syntax.GetLocation(),      // argument to remove/modify
                                actualArgumentValue.Syntax.GetLocation(),   // argument to remove/modify
                                collectionExpr!.GetLocation()),             // collection expression
                            properties: properties.ToImmutable(),
                            properAssertMethod,
                            "AreEqual"));
                    }
                    else
                    {
                        // Assert.HasCount(expectedCount, collection)
                        properties.Add(CodeFixModeKey, CodeFixModeCollectionCount);
                        SyntaxNode expectedCountExpr = expectedArgument.ConstantValue.HasValue && expectedArgument.ConstantValue.Value is int ?
                            expectedArgument.Syntax : actualArgumentValue.Syntax;

                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                            Rule,
                            additionalLocations: ImmutableArray.Create(
                                expectedArgument.Syntax.GetLocation(),      // first original argument
                                actualArgumentValue.Syntax.GetLocation(),   // second original argument
                                collectionExpr!.GetLocation(),              // collection expression
                                expectedCountExpr.GetLocation()),           // count value expression
                            properties: properties.ToImmutable(),
                            properAssertMethod,
                            "AreEqual"));
                    }

                    return;
                }
            }
        }

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

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeRemoveArgument);

            if (codeFixShouldAddCast)
            {
                properties.Add(NeedsNullableBooleanCastKey, null);
            }

            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(expectedArgument.Syntax.GetLocation(), actualArgumentValue?.Syntax.GetLocation() ?? Location.None),
                properties: properties.ToImmutable(),
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
            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeRemoveArgument);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(expectedArgument.Syntax.GetLocation()),
                properties: properties.ToImmutable(),
                properAssertMethod,
                isAreEqualInvocation ? "AreEqual" : "AreNotEqual"));
        }
    }

    private static CountCheckStatus RecognizeCountCheck(
        IOperation operation,
        INamedTypeSymbol objectTypeSymbol,
        out SyntaxNode? collectionExpression)
    {
        collectionExpression = null;

        // Check for collection.Count > 0 or collection.Length > 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan, LeftOperand: IPropertyReferenceOperation propertyRef, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef, objectTypeSymbol) is { } expression)
        {
            collectionExpression = expression;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 < collection.Count or 0 < collection.Length
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.LessThan, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef2 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef2, objectTypeSymbol) is { } expression2)
        {
            collectionExpression = expression2;
            return CountCheckStatus.HasCount;
        }

        // Check for collection.Count != 0 or collection.Length != 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: IPropertyReferenceOperation propertyRef3, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef3, objectTypeSymbol) is { } expression3)
        {
            collectionExpression = expression3;
            return CountCheckStatus.HasCount;
        }

        // Check for 0 != collection.Count or 0 != collection.Length (reverse order)
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef4 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef4, objectTypeSymbol) is { } expression4)
        {
            collectionExpression = expression4;
            return CountCheckStatus.HasCount;
        }

        // Check for collection.Count == 0 or collection.Length == 0
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, LeftOperand: IPropertyReferenceOperation propertyRef5, RightOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } } } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef5, objectTypeSymbol) is { } expression5)
        {
            collectionExpression = expression5;
            return CountCheckStatus.IsEmpty;
        }

        // Check for 0 != collection.Count or 0 != collection.Length (reverse order)
        if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.NotEquals, LeftOperand: ILiteralOperation { ConstantValue: { HasValue: true, Value: 0 } }, RightOperand: IPropertyReferenceOperation propertyRef6 } &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef6, objectTypeSymbol) is { } expression6)
        {
            collectionExpression = expression6;
            return CountCheckStatus.IsEmpty;
        }

        return CountCheckStatus.Unknown;
    }

    private static CountCheckStatus RecognizeCountCheck(
        IOperation expectedArgument,
        IOperation actualArgument,
        INamedTypeSymbol objectTypeSymbol,
        out SyntaxNode? collectionExpression,
        out SyntaxNode? countExpression,
        out int countValue)
    {
        // Check if expectedArgument is a literal and actualArgument is a count/length property
        if (expectedArgument.ConstantValue.HasValue &&
            expectedArgument.ConstantValue.Value is int expectedValue &&
            expectedValue >= 0 &&
            actualArgument is IPropertyReferenceOperation propertyRef &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef, objectTypeSymbol) is { } expression)
        {
            collectionExpression = expression;
            countExpression = propertyRef.Syntax;
            countValue = expectedValue;
            return expectedValue == 0 ? CountCheckStatus.IsEmpty : CountCheckStatus.HasCount;
        }

        // Check if actualArgument is a literal and expectedArgument is a count/length property
        if (actualArgument.ConstantValue.HasValue &&
            actualArgument.ConstantValue.Value is int actualValue &&
            actualValue >= 0 &&
            expectedArgument is IPropertyReferenceOperation propertyRef2 &&
            TryGetCollectionExpressionIfBCLCollectionLengthOrCount(propertyRef2, objectTypeSymbol) is { } expression2)
        {
            collectionExpression = expression2;
            countExpression = propertyRef2.Syntax;
            countValue = actualValue;
            return actualValue == 0 ? CountCheckStatus.IsEmpty : CountCheckStatus.HasCount;
        }

        collectionExpression = null;
        countExpression = null;
        countValue = 0;
        return CountCheckStatus.Unknown;
    }

    private static SyntaxNode? TryGetCollectionExpressionIfBCLCollectionLengthOrCount(IPropertyReferenceOperation propertyReference, INamedTypeSymbol objectTypeSymbol)
        => propertyReference.Property.Name is "Count" or "Length" &&
            propertyReference.Instance?.Type is not null &&
            (propertyReference.Instance.Type.TypeKind == TypeKind.Array || IsBCLCollectionType(propertyReference.Property.ContainingType, objectTypeSymbol))
                ? propertyReference.Instance.Syntax
                : null;

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
