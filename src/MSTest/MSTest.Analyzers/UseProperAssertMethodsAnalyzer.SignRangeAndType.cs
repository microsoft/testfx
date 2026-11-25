// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    /// <summary>
    /// Recognizes comparisons against zero that can use <c>Assert.IsPositive</c> or <c>Assert.IsNegative</c>,
    /// for example <c>x &gt; 0</c>, <c>0 &lt; x</c>, <c>x &lt; 0</c> or <c>!(x &lt;= 0)</c>.
    /// </summary>
    private static bool TryRecognizeSignCheck(
        IInvocationOperation assertion,
        IOperation condition,
        ComparisonCheckStatus comparisonStatus,
        bool isTrueInvocation,
        INamedTypeSymbol assertTypeSymbol,
        [NotNullWhen(true)] out string? properAssertMethod,
        [NotNullWhen(true)] out SyntaxNode? valueExpression)
    {
        properAssertMethod = null;
        valueExpression = null;

        if (condition is not IBinaryOperation binaryOperation ||
            !CanRewriteNewAssertion(assertion))
        {
            return false;
        }

        // Normalize Assert.IsFalse(a op b) into the equivalent strict comparison that must hold.
        ComparisonCheckStatus effectiveStatus = (isTrueInvocation, comparisonStatus) switch
        {
            (true, _) => comparisonStatus,
            (false, ComparisonCheckStatus.GreaterThanOrEqual) => ComparisonCheckStatus.LessThan,
            (false, ComparisonCheckStatus.LessThanOrEqual) => ComparisonCheckStatus.GreaterThan,
            _ => ComparisonCheckStatus.Unknown,
        };

        IOperation valueOperand;
        bool isPositive;
        if (IsZeroConstant(binaryOperation.RightOperand))
        {
            valueOperand = binaryOperation.LeftOperand;
            isPositive = effectiveStatus == ComparisonCheckStatus.GreaterThan;
        }
        else if (IsZeroConstant(binaryOperation.LeftOperand))
        {
            valueOperand = binaryOperation.RightOperand;
            isPositive = effectiveStatus == ComparisonCheckStatus.LessThan;
        }
        else
        {
            return false;
        }

        if (effectiveStatus is not (ComparisonCheckStatus.GreaterThan or ComparisonCheckStatus.LessThan))
        {
            return false;
        }

        // Numeric conversions are sign-preserving, so the value can be passed with its own type.
        valueOperand = WalkDownImplicitBuiltInConversions(valueOperand);
        if (valueOperand is IConversionOperation { IsImplicit: true, Conversion.IsUserDefined: true } ||
            valueOperand.Type is not { } valueType ||
            !IsSignedComparableNumericType(valueType.SpecialType))
        {
            return false;
        }

        // Assert.IsFalse(x <= 0) passes for NaN, whereas Assert.IsPositive(NaN) fails.
        if (!isTrueInvocation &&
            valueType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
        {
            return false;
        }

        string candidate = isPositive ? "IsPositive" : "IsNegative";
        if (!HasCompatibleGenericAssertion(assertTypeSymbol, candidate, valueParameterCount: 1))
        {
            return false;
        }

        properAssertMethod = candidate;
        valueExpression = valueOperand.Syntax;
        return true;
    }

    /// <summary>
    /// Recognizes inclusive range checks with ordered constant bounds, such as
    /// <c>lo &lt;= x &amp;&amp; x &lt;= hi</c> (in any operand order), that can use <c>Assert.IsInRange</c>.
    /// </summary>
    private static bool TryRecognizeRangeCheck(
        Compilation compilation,
        IInvocationOperation assertion,
        IOperation condition,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol assertTypeSymbol,
        [NotNullWhen(true)] out SyntaxNode? minExpression,
        [NotNullWhen(true)] out SyntaxNode? maxExpression,
        [NotNullWhen(true)] out SyntaxNode? valueExpression)
    {
        minExpression = null;
        maxExpression = null;
        valueExpression = null;

        if (condition is not IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd } conditionalAnd ||
            !CanRewriteNewAssertion(assertion) ||
            assertion.Arguments.Any(argument => !argument.IsImplicit && argument.Parameter?.Ordinal == 1 &&
                argument.Parameter.Type.SpecialType != SpecialType.System_String) ||
            !TryGetArgumentForParameterOrdinal(assertion, 0, out IArgumentOperation? conditionArgument) ||
            !ReferenceEquals(conditionArgument.Value.WalkDownBuiltInConversion(), condition) ||
            !TryGetInclusiveComparison(conditionalAnd.LeftOperand, objectTypeSymbol, out IOperation? firstSmaller, out IOperation? firstBigger) ||
            !TryGetInclusiveComparison(conditionalAnd.RightOperand, objectTypeSymbol, out IOperation? secondSmaller, out IOperation? secondBigger))
        {
            return false;
        }

        IOperation min, max, value;
        if (AreSameStableValue(firstBigger, secondSmaller))
        {
            // lo <= x && x <= hi
            (min, value, max) = (firstSmaller, firstBigger, secondBigger);
        }
        else if (AreSameStableValue(firstSmaller, secondBigger))
        {
            // x <= hi && lo <= x
            (min, value, max) = (secondSmaller, firstSmaller, firstBigger);
        }
        else
        {
            return false;
        }

        // All promoted operands must share the same type to preserve comparison semantics.
        // Floating-point types are excluded because NaN bounds compare differently with CompareTo.
        ITypeSymbol? type = value.Type;
        if (type is null ||
            value.ConstantValue.HasValue ||
            !IsRangeComparableType(type.SpecialType) ||
            !SymbolEqualityComparer.Default.Equals(min.Type, type) ||
            !SymbolEqualityComparer.Default.Equals(max.Type, type) ||
            !IsStableAssertionArgument(min) ||
            !IsStableAssertionArgument(max))
        {
            return false;
        }

        // The fixer emits the original expressions, not the promoted operands. Generic inference
        // needs a common type among those expressions, rather than a type introduced by promotion.
        ITypeSymbol? originalMinType = WalkDownImplicitBuiltInConversions(min).Type;
        ITypeSymbol? originalMaxType = WalkDownImplicitBuiltInConversions(max).Type;
        ITypeSymbol? originalValueType = WalkDownImplicitBuiltInConversions(value).Type;
        if (originalMinType is null || originalMaxType is null || originalValueType is null ||
            !(CanInferType(originalMinType) || CanInferType(originalMaxType) || CanInferType(originalValueType)))
        {
            return false;
        }

        // IsInRange throws when max < min, while the original condition simply evaluates to false.
        // Rewrite only when constant bounds prove that exception cannot be introduced.
        if (min.ConstantValue is not { HasValue: true, Value: IComparable minConstant } ||
            max.ConstantValue is not { HasValue: true, Value: { } maxConstant } ||
            minConstant.CompareTo(maxConstant) > 0)
        {
            return false;
        }

        if (!HasCompatibleGenericAssertion(assertTypeSymbol, "IsInRange", valueParameterCount: 3))
        {
            return false;
        }

        minExpression = min.Syntax;
        maxExpression = max.Syntax;
        valueExpression = value.Syntax;
        return true;

        bool CanInferType(ITypeSymbol candidate)
            => HasImplicitBuiltInConversion(originalMinType!, candidate) &&
                HasImplicitBuiltInConversion(originalMaxType!, candidate) &&
                HasImplicitBuiltInConversion(originalValueType!, candidate);

        bool HasImplicitBuiltInConversion(ITypeSymbol from, ITypeSymbol to)
        {
            CommonConversion conversion = compilation.ClassifyCommonConversion(from, to);
            return conversion.IsImplicit && !conversion.IsUserDefined;
        }

        static bool TryGetInclusiveComparison(IOperation operation, INamedTypeSymbol objectTypeSymbol, [NotNullWhen(true)] out IOperation? smaller, [NotNullWhen(true)] out IOperation? bigger)
        {
            (smaller, bigger) = operation switch
            {
                IBinaryOperation { OperatorKind: BinaryOperatorKind.LessThanOrEqual, IsLifted: false } binary when !IsExcludedOperator(binary.OperatorMethod, objectTypeSymbol)
                    => (binary.LeftOperand, binary.RightOperand),
                IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThanOrEqual, IsLifted: false } binary when !IsExcludedOperator(binary.OperatorMethod, objectTypeSymbol)
                    => (binary.RightOperand, binary.LeftOperand),
                _ => (null, null),
            };

            return smaller is not null && bigger is not null;
        }

        static bool AreSameStableValue(IOperation first, IOperation second)
            => (WalkDownImplicitBuiltInConversions(first), WalkDownImplicitBuiltInConversions(second)) switch
            {
                (ILocalReferenceOperation firstLocal, ILocalReferenceOperation secondLocal) => SymbolEqualityComparer.Default.Equals(firstLocal.Local, secondLocal.Local),
                (IParameterReferenceOperation firstParameter, IParameterReferenceOperation secondParameter) => SymbolEqualityComparer.Default.Equals(firstParameter.Parameter, secondParameter.Parameter),
                _ => false,
            };
    }

    /// <summary>
    /// Recognizes <c>collection.All(x =&gt; x is T)</c> that can use <c>Assert.AreAllOfType&lt;T&gt;</c>.
    /// </summary>
    private static bool TryRecognizeAllOfTypeCheck(
        IInvocationOperation assertion,
        IOperation condition,
        INamedTypeSymbol? enumerableTypeSymbol,
        INamedTypeSymbol assertTypeSymbol,
        [NotNullWhen(true)] out SyntaxNode? collectionExpression,
        [NotNullWhen(true)] out SyntaxNode? typeCheckExpression)
    {
        collectionExpression = null;
        typeCheckExpression = null;

        // AreAllOfType enumerates the whole collection; only arrays are known not to have a lazy or throwing iterator.
        if (RecognizeLinqPredicateCheck(condition, enumerableTypeSymbol, out SyntaxNode? collection, out _, out _) != LinqPredicateCheckStatus.All ||
            collection is null ||
            !CanRewriteAllWithoutReorderingEffects(assertion, condition) ||
            !TryGetArgumentForParameterOrdinal((IInvocationOperation)condition, 0, out IArgumentOperation? collectionArgument) ||
            WalkDownImplicitBuiltInConversions(collectionArgument.Value).Type is not IArrayTypeSymbol ||
            !TryGetArgumentForParameterOrdinal((IInvocationOperation)condition, 1, out IArgumentOperation? predicateArgument))
        {
            return false;
        }

        IOperation predicate = predicateArgument.Value;
        while (predicate is IConversionOperation { OperatorMethod: null } conversion)
        {
            predicate = conversion.Operand;
        }

        if (predicate is IDelegateCreationOperation delegateCreation)
        {
            predicate = delegateCreation.Target;
        }

        // Only expression-bodied lambdas of the form 'x => x is T' are recognized.
        if (predicate is not IAnonymousFunctionOperation { Symbol.Parameters.Length: 1 } lambda ||
            lambda.Body.Operations.Length != 1 ||
            lambda.Body.Operations[0] is not IReturnOperation { IsImplicit: true, ReturnedValue: IIsTypeOperation { IsNegated: false } isTypeOperation } ||
            isTypeOperation.ValueOperand is not IParameterReferenceOperation parameterReference ||
            !SymbolEqualityComparer.Default.Equals(parameterReference.Parameter, lambda.Symbol.Parameters[0]))
        {
            return false;
        }

        ITypeSymbol checkedType = isTypeOperation.TypeOperand;
        if (checkedType is IPointerTypeSymbol or IFunctionPointerTypeSymbol or IErrorTypeSymbol ||
            checkedType.IsRefLikeType ||
            checkedType.IsStatic ||
            checkedType.SpecialType == SpecialType.System_Void ||
            !HasCompatibleAssertion(assertTypeSymbol, "AreAllOfType", arity: 1, SpecialType.System_Collections_IEnumerable))
        {
            return false;
        }

        collectionExpression = collection;
        typeCheckExpression = isTypeOperation.Syntax;
        return true;
    }

    private static bool HasCompatibleGenericAssertion(INamedTypeSymbol assertType, string name, int valueParameterCount)
        => assertType.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public &&
            method.Arity == 1 &&
            method.Parameters.Length > valueParameterCount &&
            method.Parameters.Take(valueParameterCount).All(parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, method.TypeParameters[0])) &&
            method.Parameters[valueParameterCount].Type.SpecialType == SpecialType.System_String &&
            method.Parameters[valueParameterCount].IsOptional);

    private static IOperation WalkDownImplicitBuiltInConversions(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true, Conversion.IsUserDefined: false } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsZeroConstant(IOperation operation)
        => operation.ConstantValue is { HasValue: true, Value: var value } &&
            value switch
            {
                sbyte v => v == 0,
                byte v => v == 0,
                short v => v == 0,
                ushort v => v == 0,
                int v => v == 0,
                uint v => v == 0,
                long v => v == 0,
                ulong v => v == 0,
                float v => v == 0,
                double v => v == 0,
                decimal v => v == 0,
                _ => false,
            };

    private static bool IsSignedComparableNumericType(SpecialType specialType)
        => specialType is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Single or SpecialType.System_Double
            or SpecialType.System_Decimal;

    private static bool IsRangeComparableType(SpecialType specialType)
        => specialType is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64 or SpecialType.System_UInt64
            or SpecialType.System_Char or SpecialType.System_Decimal;
}
