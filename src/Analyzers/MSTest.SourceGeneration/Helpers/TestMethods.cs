// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration;

internal static class TestMethods
{
    public static SymbolDisplayFormat MethodIdentifierFullyQualifiedTypeFormat { get; } =
        SymbolDisplayFormat.CSharpErrorMessageFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
            SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static bool IsValidTestMethodShape(this IMethodSymbol methodSymbol, WellKnownTypes wellKnownTypes)
    {
        // We only look for public methods
        if (methodSymbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        // We don't support generic test methods
        if (!methodSymbol.TypeParameters.IsEmpty)
        {
            return false;
        }

        // We don't support static test methods
        if (methodSymbol.IsStatic)
        {
            return false;
        }

        // We accept only simple methods
        if (methodSymbol.MethodKind != MethodKind.Ordinary
            || methodSymbol.IsAbstract
            || methodSymbol.IsExtern
            || methodSymbol.IsVirtual
            || methodSymbol.IsOverride
            || methodSymbol.IsImplicitlyDeclared
            || methodSymbol.IsPartialDefinition)
        {
            return false;
        }

        // We don't support async void
        if (methodSymbol is { ReturnsVoid: true, IsAsync: true })
        {
            return false;
        }

        // We support only void and Task return methods
        if (!methodSymbol.ReturnsVoid
            && !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, wellKnownTypes.TaskSymbol)
            && !SymbolEqualityComparer.Default.Equals(methodSymbol.ReturnType, wellKnownTypes.ValueTaskSymbol))
        {
            return false;
        }

        // Method has correct shape to be a test method
        return true;
    }

    /// <summary>
    /// Method has a test method shape but is known to not be a test method.
    /// </summary>
    public static bool IsKnownNonTestMethod(this IMethodSymbol methodSymbol, WellKnownTypes wellKnownTypes)
        => methodSymbol.IsDisposeImplementation(wellKnownTypes.IDisposableSymbol)
        || methodSymbol.IsAsyncDisposeImplementation(wellKnownTypes.IAsyncDisposableSymbol, wellKnownTypes.ValueTaskSymbol)
        || methodSymbol.GetAttributes().Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, wellKnownTypes.IgnoreAttributeSymbol));
}
