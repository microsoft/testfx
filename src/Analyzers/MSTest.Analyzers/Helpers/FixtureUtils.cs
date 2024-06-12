// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureUtils
{
    public static bool IsAssemblyInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol assemblyInitializeAttributeSymbol)
    => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, assemblyInitializeAttributeSymbol));

    public static bool IsAssemblyCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol assemblyCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, assemblyCleanupAttributeSymbol));

    public static bool IsClassInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol classInitializeAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classInitializeAttributeSymbol));

    public static bool IsClassCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol classCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classCleanupAttributeSymbol));

    public static bool IsTestInitializeMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol testInitializeAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testInitializeAttributeSymbol));

    public static bool IsTestCleanupMethod(this IMethodSymbol methodSymbol, INamedTypeSymbol testCleanupAttributeSymbol)
        => methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, testCleanupAttributeSymbol));

    public static bool HasValidFixtureMethodSignature(this IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals, bool shouldBeStatic, bool allowGenericType,
        INamedTypeSymbol? testContextSymbol, out bool isFixable)
    {
        if (methodSymbol.MethodKind != MethodKind.Ordinary
            || (methodSymbol.ContainingType.IsGenericType && !allowGenericType))
        {
            isFixable = false;
            return false;
        }

        isFixable = true;
        return !methodSymbol.IsGenericMethod
            && methodSymbol.IsStatic == shouldBeStatic
            && !methodSymbol.IsAbstract
            && HasCorrectParameters(methodSymbol, testContextSymbol)
            && methodSymbol.IsPublicAndHasCorrectResultantVisibility(canDiscoverInternals)
            && HasValidReturnType(methodSymbol, taskSymbol, valueTaskSymbol);
    }

    public static bool HasValidTestMethodSignature(this IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol,
    INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        if (methodSymbol.GetResultantVisibility() is { } resultantVisibility)
        {
            if (!canDiscoverInternals && (resultantVisibility != SymbolVisibility.Public || methodSymbol.DeclaredAccessibility != Accessibility.Public))
            {
                return false;
            }
            else if (canDiscoverInternals && resultantVisibility == SymbolVisibility.Private)
            {
                return false;
            }
        }

        if (methodSymbol is { ReturnsVoid: true, IsAsync: true }
            || (!methodSymbol.ReturnsVoid
            && (taskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
            && (valueTaskSymbol is null || !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol))))
        {
            return false;
        }

        return methodSymbol.MethodKind == MethodKind.Ordinary
            && !methodSymbol.IsGenericMethod
            && !methodSymbol.IsStatic
            && !methodSymbol.IsAbstract;
    }

    public static bool IsInheritanceModeSet(this IMethodSymbol methodSymbol, INamedTypeSymbol? inheritanceBehaviorSymbol,
        INamedTypeSymbol? classInitializeOrCleanupAttributeSymbol)
    {
        foreach (AttributeData attr in methodSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, classInitializeOrCleanupAttributeSymbol))
            {
                continue;
            }

            ImmutableArray<TypedConstant> constructorArguments = attr.ConstructorArguments;
            foreach (TypedConstant constructorArgument in constructorArguments)
            {
                if (!SymbolEqualityComparer.Default.Equals(constructorArgument.Type, inheritanceBehaviorSymbol))
                {
                    continue;
                }

                // It's an enum so it can't be null
                RoslynDebug.Assert(constructorArgument.Value is not null);

                // We need to check that the inheritanceBehavior is not set to none and it's value inside the enum is zero
                if ((int)constructorArgument.Value != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasCorrectParameters(IMethodSymbol methodSymbol, INamedTypeSymbol? testContextSymbol)
        => testContextSymbol is null
            ? methodSymbol.Parameters.Length == 0
            : methodSymbol.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, testContextSymbol);

    private static bool HasValidReturnType(IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        => methodSymbol is { ReturnsVoid: true, IsAsync: false }
        || (taskSymbol is not null && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
        || (valueTaskSymbol is not null && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol));
}
