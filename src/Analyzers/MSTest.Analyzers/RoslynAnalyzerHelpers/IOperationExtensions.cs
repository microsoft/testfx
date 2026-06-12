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

    /// <summary>
    /// Walks down consecutive built-in conversion operations, stopping at user-defined
    /// conversions or non-conversion operands.
    /// </summary>
    /// <param name="operation">The starting operation.</param>
    /// <returns>
    /// The first operand that is either a user-defined conversion or not a conversion at all,
    /// or the starting operation if it was already one of those.
    /// </returns>
    public static IOperation WalkDownBuiltInConversion(this IOperation operation)
    {
        while (operation is IConversionOperation conversionOperation && !conversionOperation.Conversion.IsUserDefined)
        {
            operation = conversionOperation.Operand;
        }

        return operation;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the two operations are structurally equivalent
    /// side-effect-free references to the same local, parameter, field, property, or <c>this</c>.
    /// </summary>
    /// <remarks>
    /// This intentionally excludes method invocations and indexer accesses, since those may
    /// have side effects or return different values on repeated evaluation. Parenthesized
    /// expressions and conversions are skipped transparently.
    /// </remarks>
    public static bool IsEquivalentReferenceTo(this IOperation? left, IOperation? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        left = Unwrap(left);
        right = Unwrap(right);

        return (left, right) switch
        {
            (ILocalReferenceOperation la, ILocalReferenceOperation lb) =>
                SymbolEqualityComparer.Default.Equals(la.Local, lb.Local),

            (IParameterReferenceOperation pa, IParameterReferenceOperation pb) =>
                SymbolEqualityComparer.Default.Equals(pa.Parameter, pb.Parameter),

            (IFieldReferenceOperation fa, IFieldReferenceOperation fb) =>
                SymbolEqualityComparer.Default.Equals(fa.Field, fb.Field) &&
                AreInstancesEquivalent(fa.Instance, fb.Instance),

            (IPropertyReferenceOperation pra, IPropertyReferenceOperation prb) =>
                SymbolEqualityComparer.Default.Equals(pra.Property, prb.Property) &&
                AreInstancesEquivalent(pra.Instance, prb.Instance),

            (IInstanceReferenceOperation, IInstanceReferenceOperation) => true,

            _ => false,
        };

        static IOperation Unwrap(IOperation operation)
        {
            while (true)
            {
                switch (operation)
                {
                    case IParenthesizedOperation parenthesized:
                        operation = parenthesized.Operand;
                        break;
                    case IConversionOperation conversion:
                        operation = conversion.Operand;
                        break;
                    default:
                        return operation;
                }
            }
        }

        static bool AreInstancesEquivalent(IOperation? a, IOperation? b)
            => (a is null && b is null) || IsEquivalentReferenceTo(a, b);
    }
}
