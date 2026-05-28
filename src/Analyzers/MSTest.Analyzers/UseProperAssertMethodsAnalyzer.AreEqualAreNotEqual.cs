// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private static void AnalyzeAreEqualOrAreNotEqualInvocation(OperationAnalysisContext context, IOperation expectedArgument, bool isAreEqualInvocation, INamedTypeSymbol objectTypeSymbol, INamedTypeSymbol? enumerableTypeSymbol)
    {
        // Check for collection count patterns: collection.Count/Length == 0 or collection.Count/Length == X
        if (isAreEqualInvocation)
        {
            if (TryGetSecondArgumentValue((IInvocationOperation)context.Operation, out IOperation? actualArgumentValue) &&
                TryGetArgumentForParameterOrdinal((IInvocationOperation)context.Operation, 1, out IArgumentOperation? actualArgument))
            {
                // Check for LINQ predicate patterns that suggest ContainsSingle
                LinqPredicateCheckStatus linqStatus2 = RecognizeLinqPredicateCheck(
                    actualArgumentValue,
                    enumerableTypeSymbol,
                    out SyntaxNode? linqCollectionExpr2,
                    out SyntaxNode? predicateExpr2,
                    out _);

                if (isAreEqualInvocation &&
                    linqStatus2 is LinqPredicateCheckStatus.Count or LinqPredicateCheckStatus.WhereCount &&
                    linqCollectionExpr2 != null &&
                    predicateExpr2 != null &&
                    expectedArgument.ConstantValue.HasValue &&
                    expectedArgument.ConstantValue.Value is int expectedCountValue &&
                    expectedCountValue == 1)
                {
                    // We have Assert.AreEqual(1, enumerable.Count(predicate))
                    // We want Assert.ContainsSingle(predicate, enumerable)
                    string properAssertMethod = "ContainsSingle";

                    ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(ProperAssertMethodNameKey, properAssertMethod);
                    properties.Add(CodeFixModeKey, CodeFixModeRemoveArgumentReplaceArgumentAndAddArgument);
                    context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                        Rule,
                        additionalLocations: ImmutableArray.Create(
                            expectedArgument.Syntax.GetLocation(),
                            actualArgument.Syntax.GetLocation(),
                            predicateExpr2.GetLocation(),
                            linqCollectionExpr2.GetLocation()),
                        properties: properties.ToImmutable(),
                        properAssertMethod,
                        "AreEqual"));
                    return;
                }

                // Check if we're comparing a count/length property
                CountCheckStatus countStatus = RecognizeCountCheck(
                    expectedArgument,
                    actualArgumentValue,
                    objectTypeSymbol,
                    enumerableTypeSymbol,
                    out SyntaxNode? nodeToBeReplaced1,
                    out SyntaxNode? replacement1,
                    out SyntaxNode? nodeToBeReplaced2,
                    out SyntaxNode? replacement2);

                if (countStatus != CountCheckStatus.Unknown)
                {
                    if (nodeToBeReplaced1 is null || replacement1 is null)
                    {
                        throw ApplicationStateGuard.Unreachable();
                    }

                    string properAssertMethod = countStatus == CountCheckStatus.IsEmpty ? "IsEmpty" : "HasCount";

                    ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                    properties.Add(ProperAssertMethodNameKey, properAssertMethod);

                    if (nodeToBeReplaced2 is not null && replacement2 is null)
                    {
                        // Here we suggest Assert.IsEmpty(collection)
                        properties.Add(CodeFixModeKey, CodeFixModeRemoveArgumentAndReplaceArgument);
                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                            Rule,
                            additionalLocations: ImmutableArray.Create(
                                nodeToBeReplaced2.GetLocation(),
                                nodeToBeReplaced1.GetLocation(),
                                replacement1.GetLocation()),
                            properties: properties.ToImmutable(),
                            properAssertMethod,
                            "AreEqual"));
                    }
                    else
                    {
                        // Here we suggest Assert.HasCount(expectedCount, collection)
                        properties.Add(CodeFixModeKey, CodeFixModeSimple);
                        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                            Rule,
                            additionalLocations: nodeToBeReplaced2 is not null && replacement2 is not null
                                ? ImmutableArray.Create(nodeToBeReplaced1.GetLocation(), replacement1.GetLocation(), nodeToBeReplaced2.GetLocation(), replacement2.GetLocation())
                                : ImmutableArray.Create(nodeToBeReplaced1.GetLocation(), replacement1.GetLocation()),
                            properties: properties.ToImmutable(),
                            properAssertMethod,
                            "AreEqual"));
                    }

                    return;
                }
            }
        }

        // Check for AreNotEqual(0, collection.Count/Length) or AreNotEqual(0, enumerable.Count()) → IsNotEmpty
        if (!isAreEqualInvocation &&
            TryGetSecondArgumentValue((IInvocationOperation)context.Operation, out IOperation? actualArgumentValueNotEqual))
        {
            CountCheckStatus notEqualCountStatus = RecognizeCountCheck(
                expectedArgument,
                actualArgumentValueNotEqual,
                objectTypeSymbol,
                enumerableTypeSymbol,
                out SyntaxNode? nodeToBeReplacedNE1,
                out SyntaxNode? replacementNE1,
                out SyntaxNode? nodeToBeReplacedNE2,
                out _);

            // We only handle IsEmpty (i.e. AreNotEqual(0, count) → IsNotEmpty).
            // HasCount is intentionally not handled: there's no semantic equivalent for
            // AreNotEqual(N, count) where N != 0.
            if (notEqualCountStatus == CountCheckStatus.IsEmpty)
            {
                if (nodeToBeReplacedNE1 is null || replacementNE1 is null || nodeToBeReplacedNE2 is null)
                {
                    throw ApplicationStateGuard.Unreachable();
                }

                // AreNotEqual(0, collection.Count/Length/Count()) → IsNotEmpty(collection)
                ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add(ProperAssertMethodNameKey, "IsNotEmpty");
                properties.Add(CodeFixModeKey, CodeFixModeRemoveArgumentAndReplaceArgument);
                context.ReportDiagnostic(context.Operation.CreateDiagnostic(
                    Rule,
                    additionalLocations: ImmutableArray.Create(
                        nodeToBeReplacedNE2.GetLocation(),
                        nodeToBeReplacedNE1.GetLocation(),
                        replacementNE1.GetLocation()),
                    properties: properties.ToImmutable(),
                    "IsNotEmpty",
                    "AreNotEqual"));
                return;
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
}
