// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

using MSTest.Analyzers.RoslynAnalyzerHelpers;

namespace MSTest.Analyzers;

public sealed partial class UseProperAssertMethodsAnalyzer
{
    private static bool CanUseTypeAsObject(Compilation compilation, ITypeSymbol? type)
        => type is null ||
            compilation.ClassifyCommonConversion(type, compilation.GetSpecialType(SpecialType.System_Object)).Exists;

    private static bool IsExcludedOperator(IMethodSymbol? operatorSymbol, INamedTypeSymbol objectTypeSymbol)
        // We exclude user-defined operators from analysis. But only if they are "really" user-defined (not from BCL)
        => operatorSymbol?.MethodKind == MethodKind.UserDefinedOperator && !IsBCLSymbol(operatorSymbol, objectTypeSymbol);

    private static bool IsBCLSymbol(ISymbol symbol, INamedTypeSymbol objectTypeSymbol)
        // object is coming from BCL and it's expected to always have a public key.
        => symbol.ContainingAssembly.Identity.HasPublicKey == objectTypeSymbol.ContainingAssembly.Identity.HasPublicKey &&
            symbol.ContainingAssembly.Identity.PublicKey.SequenceEqual(objectTypeSymbol.ContainingAssembly.Identity.PublicKey);

    private static bool TryGetFirstArgumentValue(IInvocationOperation operation, [NotNullWhen(true)] out IOperation? argumentValue)
        => TryGetArgumentValueForParameterOrdinal(operation, 0, out argumentValue);

    private static bool TryGetSecondArgumentValue(IInvocationOperation operation, [NotNullWhen(true)] out IOperation? argumentValue)
        => TryGetArgumentValueForParameterOrdinal(operation, 1, out argumentValue);

    private static bool TryGetArgumentForParameterOrdinal(IInvocationOperation operation, int ordinal, [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        argument = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == ordinal);
        return argument is not null;
    }

    private static bool TryGetArgumentValueForParameterOrdinal(IInvocationOperation operation, int ordinal, [NotNullWhen(true)] out IOperation? argumentValue)
    {
        argumentValue = operation.Arguments.FirstOrDefault(arg => arg.Parameter?.Ordinal == ordinal)?.Value?.WalkDownConversion();
        return argumentValue is not null;
    }
}
