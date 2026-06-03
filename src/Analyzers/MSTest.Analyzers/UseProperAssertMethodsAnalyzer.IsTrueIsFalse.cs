// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private static void AnalyzeIsTrueOrIsFalseInvocation(OperationAnalysisContext context, IOperation conditionArgument, bool isTrueInvocation, INamedTypeSymbol objectTypeSymbol, INamedTypeSymbol? enumerableTypeSymbol, INamedTypeSymbol? iComparableOfTSymbol)
    {
        RoslynDebug.Assert(context.Operation is IInvocationOperation, "Expected IInvocationOperation.");

        NullCheckStatus nullCheckStatus = RecognizeNullCheck(conditionArgument, objectTypeSymbol, out SyntaxNode? expressionUnderTest, out ITypeSymbol? typeOfExpressionUnderTest);

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

        // Check for LINQ predicate patterns that suggest Contains/DoesNotContain
        LinqPredicateCheckStatus linqStatus = RecognizeLinqPredicateCheck(
            conditionArgument,
            enumerableTypeSymbol,
            out SyntaxNode? linqCollectionExpr,
            out SyntaxNode? predicateExpr,
            out _);

        // For Any() and Where().Any() patterns
        if (linqStatus is LinqPredicateCheckStatus.Any or LinqPredicateCheckStatus.WhereAny &&
            linqCollectionExpr != null && predicateExpr != null)
        {
            string properAssertMethod = isTrueInvocation ? "Contains" : "DoesNotContain";

            ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add(ProperAssertMethodNameKey, properAssertMethod);
            properties.Add(CodeFixModeKey, CodeFixModeAddArgument);
            context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                Rule,
                additionalLocations: ImmutableArray.Create(
                    conditionArgument.Syntax.GetLocation(),
                    predicateExpr.GetLocation(),
                    linqCollectionExpr.GetLocation()),
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
        CollectionCheckStatus collectionMethodStatus = RecognizeCollectionMethodCheck(conditionArgument, objectTypeSymbol, enumerableTypeSymbol, out SyntaxNode? collectionExpr, out SyntaxNode? itemExpr, out SyntaxNode? comparerExpr);
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

            if (collectionMethodStatus == CollectionCheckStatus.ContainsWithComparer)
            {
                string properAssertMethod = isTrueInvocation ? "Contains" : "DoesNotContain";

                ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(ProperAssertMethodNameKey, properAssertMethod);
                properties.Add(CodeFixModeKey, CodeFixModeAddTwoArguments);
                context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                    Rule,
                    additionalLocations: ImmutableArray.Create(conditionArgument.Syntax.GetLocation(), itemExpr!.GetLocation(), collectionExpr!.GetLocation(), comparerExpr!.GetLocation()),
                    properties: properties.ToImmutable(),
                    properAssertMethod,
                    isTrueInvocation ? "IsTrue" : "IsFalse"));
                return;
            }
        }

        // Check for collection emptiness patterns: myCollection.Count > 0, myCollection.Count != 0, or myCollection.Count == 0
        CountCheckStatus countStatus = RecognizeCountCheck(conditionArgument, objectTypeSymbol, enumerableTypeSymbol, out SyntaxNode? collectionEmptinessExpr);
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

        // Special-case: enumerable.Count(predicate) > 0 → Assert.Contains(predicate, enumerable)
        if (conditionArgument is IBinaryOperation binaryOp &&
            binaryOp.OperatorKind == BinaryOperatorKind.GreaterThan &&
            binaryOp.RightOperand.ConstantValue.HasValue &&
            binaryOp.RightOperand.ConstantValue.Value is int intValue &&
            intValue == 0)
        {
            // Use RecognizeLinqPredicateCheck to properly validate LINQ Count method
            LinqPredicateCheckStatus countLinqStatus = RecognizeLinqPredicateCheck(
                binaryOp.LeftOperand,
                enumerableTypeSymbol,
                out SyntaxNode? countCollectionExpr,
                out SyntaxNode? countPredicateExpr,
                out _);

            if ((countLinqStatus is LinqPredicateCheckStatus.Count or LinqPredicateCheckStatus.WhereCount) &&
                countCollectionExpr != null &&
                countPredicateExpr != null)
            {
                string properAssertMethod = isTrueInvocation ? "Contains" : "DoesNotContain";

                ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(ProperAssertMethodNameKey, properAssertMethod);
                properties.Add(CodeFixModeKey, CodeFixModeAddArgument);

                context.ReportDiagnostic(
                    context.Operation.CreateDiagnostic(
                        Rule,
                        additionalLocations: ImmutableArray.Create(
                            conditionArgument.Syntax.GetLocation(),
                            countPredicateExpr.GetLocation(),
                            countCollectionExpr.GetLocation()),
                        properties: properties.ToImmutable(),
                        properAssertMethod,
                        isTrueInvocation ? "IsTrue" : "IsFalse"));

                return;
            }
        }

        // Check for comparison patterns: a > b, a >= b, a < b, a <= b
        ComparisonCheckStatus comparisonStatus = RecognizeComparisonCheck(conditionArgument, objectTypeSymbol, iComparableOfTSymbol, out SyntaxNode? leftExpr, out SyntaxNode? rightExpr);
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

        EqualityCheckStatus equalityCheckStatus = RecognizeEqualityCheck(conditionArgument, objectTypeSymbol, out SyntaxNode? toBecomeExpected, out SyntaxNode? toBecomeActual, out ITypeSymbol? leftType, out ITypeSymbol? rightType);
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
}
