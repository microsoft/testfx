// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private enum StringMethodCheckStatus
    {
        Unknown,
        StartsWith,
        EndsWith,
        Contains,
    }

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
            if (methodName is "StartsWith" or "EndsWith" or "Contains" &&
                invocation.Arguments.Length > 0 &&
                invocation.Arguments[0].Parameter?.Type.SpecialType == SpecialType.System_String)
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
}
