// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Shared;

internal static class SymbolAccessibilityHelper
{
    public static bool IsAccessibleFromGeneratedCode(INamedTypeSymbol type)
    {
        // The generated code lives in the same assembly but in a different file/type,
        // so it can reach Public / Internal / ProtectedOrInternal types (the latter being
        // "protected internal" — visible from anywhere in the same assembly). Private,
        // Protected (alone), and ProtectedAndInternal ("private protected") containing
        // types make the type unreachable.
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.NotApplicable:
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }
}
