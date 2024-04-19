// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.RoslynAnalyzerHelpers;
internal static class IOperationExtensions
{
    public static ISymbol? GetReferencedMemberOrLocalOrParameter(this IOperation? operation)
    {
        return operation switch
        {
            IMemberReferenceOperation memberReference => memberReference.Member,

            IParameterReferenceOperation parameterReference => parameterReference.Parameter,

            ILocalReferenceOperation localReference => localReference.Local,

            IParenthesizedOperation parenthesized => parenthesized.Operand.GetReferencedMemberOrLocalOrParameter(),

            IConversionOperation conversion => conversion.Operand.GetReferencedMemberOrLocalOrParameter(),

            _ => null,
        };
    }
}
