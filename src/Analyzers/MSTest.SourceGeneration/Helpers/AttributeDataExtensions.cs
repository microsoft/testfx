// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration.Helpers;

internal static class AttributeDataExtensions
{
    public static bool TryGetTestExecutionTimeout(this AttributeData attribute, INamedTypeSymbol? executionTimeoutAttributeSymbol,
        INamedTypeSymbol? timeSpanSymbol, out TimeSpan executionTimeout)
    {
        if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, executionTimeoutAttributeSymbol))
        {
            executionTimeout = default;
            return false;
        }

        return TryGetTimeoutValue(attribute, timeSpanSymbol, out executionTimeout);
    }

    private static bool TryGetTimeoutValue(this AttributeData attribute, INamedTypeSymbol? timeSpanSymbol, out TimeSpan timeout)
    {
        if (attribute.ConstructorArguments.Length == 1
            && attribute.ConstructorArguments[0].Type is { } executionTimeoutCtorArgType
            && attribute.ConstructorArguments[0].Value is { } executionTimeoutCtorArgValue)
        {
            if (executionTimeoutCtorArgType.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64)
            {
                timeout = TimeSpan.FromMilliseconds((int)executionTimeoutCtorArgValue);
                return true;
            }
            else if (SymbolEqualityComparer.Default.Equals(executionTimeoutCtorArgType, timeSpanSymbol))
            {
                timeout = (TimeSpan)executionTimeoutCtorArgValue;
                return true;
            }
        }

        timeout = default;
        return false;
    }
}
