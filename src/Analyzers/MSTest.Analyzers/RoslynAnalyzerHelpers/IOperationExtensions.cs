// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MSTest.Analyzers.RoslynAnalyzerHelpers;
internal static class IOperationExtensions
{
    public static ISymbol? GetReferencedMemberOrLocalOrParameter(this IOperation? operation) => operation switch
    {
        IMemberReferenceOperation memberReference => memberReference.Member,

        IParameterReferenceOperation parameterReference => parameterReference.Parameter,

        ILocalReferenceOperation localReference => localReference.Local,

        IParenthesizedOperation parenthesized => parenthesized.Operand.GetReferencedMemberOrLocalOrParameter(),

        IConversionOperation conversion => conversion.Operand.GetReferencedMemberOrLocalOrParameter(),

        _ => null,
    };

    /// <summary>
    /// Walks down consecutive conversion operations until an operand is reached that isn't a conversion operation.
    /// </summary>
    /// <param name="operation">The starting operation.</param>
    /// <returns>The inner non conversion operation or the starting operation if it wasn't a conversion operation.</returns>
    public static IOperation WalkDownConversion(this IOperation operation)
    {
        while (operation is IConversionOperation conversionOperation)
        {
            operation = conversionOperation.Operand;
        }

        return operation;
    }
}
