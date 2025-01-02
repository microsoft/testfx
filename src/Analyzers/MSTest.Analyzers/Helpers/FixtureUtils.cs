// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers.Helpers;

internal static class FixtureUtils
{
    public static bool HasValidFixtureMethodSignature(this IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals, bool shouldBeStatic, bool allowGenericType,
        FixtureParameterMode fixtureParameterMode,
        INamedTypeSymbol? testContextSymbol, INamedTypeSymbol testClassAttributeSymbol, bool fixtureAllowInheritedTestClass, out bool isFixable)
    {
        isFixable = false;
        if (methodSymbol.MethodKind != MethodKind.Ordinary
            || (methodSymbol.ContainingType.IsGenericType && !allowGenericType))
        {
            return false;
        }

        // Fixtures are only supported on classes
        if (methodSymbol.ContainingType.TypeKind != TypeKind.Class)
        {
            return false;
        }

        // For AssemblyInitialize and AssemblyCleanup methods, the containing class should be marked with TestClassAttribute.
        // For the other fixtures, it's only required if the type is not sealed.
        if ((!fixtureAllowInheritedTestClass || methodSymbol.ContainingType.IsSealed)
            && !methodSymbol.ContainingType.GetAttributes().Any(x => x.AttributeClass.Inherits(testClassAttributeSymbol)))
        {
            return false;
        }

        // Validate the method signature
        isFixable = true;
        return !methodSymbol.IsGenericMethod
            && methodSymbol.IsStatic == shouldBeStatic
            && !methodSymbol.IsAbstract
            && HasCorrectParameters(methodSymbol, fixtureParameterMode, testContextSymbol)
            && methodSymbol.IsPublicAndHasCorrectResultantVisibility(canDiscoverInternals)
            && HasValidReturnType(methodSymbol, taskSymbol, valueTaskSymbol);
    }

    public static bool HasValidTestMethodSignature(this IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol,
        INamedTypeSymbol? valueTaskSymbol, bool canDiscoverInternals)
    {
        if (methodSymbol.MethodKind != MethodKind.Ordinary
            || methodSymbol.IsGenericMethod
            || methodSymbol.IsStatic
            || methodSymbol.IsAbstract)
        {
            return false;
        }

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

        return methodSymbol is { ReturnsVoid: true, IsAsync: false }
                || SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol)
                || SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol);
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

    private static bool HasCorrectParameters(IMethodSymbol methodSymbol, FixtureParameterMode fixtureParameterMode, INamedTypeSymbol? testContextSymbol)
    {
        return fixtureParameterMode switch
        {
            FixtureParameterMode.MustNotHaveTestContext => DoesNotHaveTestContext(methodSymbol),
            FixtureParameterMode.MustHaveTestContext => HasTestContext(methodSymbol, testContextSymbol),
            FixtureParameterMode.OptionalTestContext => DoesNotHaveTestContext(methodSymbol) || HasTestContext(methodSymbol, testContextSymbol),
            _ => throw ApplicationStateGuard.Unreachable(),
        };

        static bool DoesNotHaveTestContext(IMethodSymbol methodSymbol)
            => methodSymbol.Parameters.Length == 0;

        static bool HasTestContext(IMethodSymbol methodSymbol, INamedTypeSymbol? testContextSymbol)
            => testContextSymbol is not null && methodSymbol.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[0].Type, testContextSymbol);
    }

    private static bool HasValidReturnType(IMethodSymbol methodSymbol, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        => methodSymbol is { ReturnsVoid: true, IsAsync: false }
        || (taskSymbol is not null && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, taskSymbol))
        || (valueTaskSymbol is not null && SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, valueTaskSymbol));
}
