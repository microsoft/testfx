// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private static void AnalyzeHasCountInvocation(OperationAnalysisContext context, IOperation expectedArgument)
    {
        // We want to flag Assert.HasCount(0, collection) and suggest Assert.IsEmpty(collection).
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol targetMethod = invocation.TargetMethod;

        // HasCount overloads are HasCount(int expected, TCollection collection, ...), so the
        // collection is the second parameter (ordinal 1).
        if (targetMethod.Parameters.Length < 2)
        {
            return;
        }

        // Assert.IsEmpty only has IEnumerable-based overloads. The Span/ReadOnlySpan/Memory/ReadOnlyMemory
        // HasCount overloads have no IsEmpty equivalent, so we must not suggest a fix that would not compile.
        if (IsSpanOrMemoryType(targetMethod.Parameters[1].Type))
        {
            return;
        }

        // Only suggest IsEmpty when the expected count is the constant 0.
        if (!expectedArgument.ConstantValue.HasValue ||
            expectedArgument.ConstantValue.Value is not int expectedCount ||
            expectedCount != 0)
        {
            return;
        }

        // Assert.HasCount(0, collection) -> Assert.IsEmpty(collection).
        // The codefix removes the expected (0) argument and renames HasCount to IsEmpty.
        ImmutableDictionary<string, string?>.Builder properties = ImmutableDictionary.CreateBuilder<string, string?>();
        properties.Add(ProperAssertMethodNameKey, "IsEmpty");
        properties.Add(CodeFixModeKey, CodeFixModeRemoveArgument);
        context.ReportDiagnostic(context.Operation.CreateDiagnostic(
            Rule,
            additionalLocations: ImmutableArray.Create(expectedArgument.Syntax.GetLocation()),
            properties: properties.ToImmutable(),
            "IsEmpty",
            "HasCount"));
    }
}
