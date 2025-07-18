// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.Testing.Framework.SourceGeneration;

internal static class IMethodSymbolExtensions
{/// <summary>
 /// Checks if the given method implements <see cref="IDisposable.Dispose"/> or overrides an implementation of <see cref="IDisposable.Dispose"/>.
 /// </summary>
    public static bool IsDisposeImplementation(this IMethodSymbol? method, INamedTypeSymbol? iDisposable)
    {
        if (method is null)
        {
            return false;
        }

        if (method.IsOverride)
        {
            return method.OverriddenMethod.IsDisposeImplementation(iDisposable);
        }

        // Identify the implementor of IDisposable.Dispose in the given method's containing type and check
        // if it is the given method.
        return method is { ReturnsVoid: true, Parameters.IsEmpty: true }
               && method.IsImplementationOfInterfaceMethod(null, iDisposable, "Dispose");
    }

    /// <summary>
    /// Checks if the given method implements "IAsyncDisposable.Dispose" or overrides an implementation of "IAsyncDisposable.Dispose".
    /// </summary>
    public static bool IsAsyncDisposeImplementation(this IMethodSymbol? method, INamedTypeSymbol? iAsyncDisposable, INamedTypeSymbol? valueTaskType)
    {
        if (method is null)
        {
            return false;
        }

        if (method.IsOverride)
        {
            return method.OverriddenMethod.IsAsyncDisposeImplementation(iAsyncDisposable, valueTaskType);
        }

        // Identify the implementor of IAsyncDisposable.Dispose in the given method's containing type and check
        // if it is the given method.
        return SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskType)
            && method.Parameters.IsEmpty
            && method.IsImplementationOfInterfaceMethod(null, iAsyncDisposable, "DisposeAsync");
    }

    /// <summary>
    /// Checks if the given method is an implementation of the given interface method
    /// Substituted with the given typeargument.
    /// </summary>
    public static bool IsImplementationOfInterfaceMethod(this IMethodSymbol method, ITypeSymbol? typeArgument, INamedTypeSymbol? interfaceType, string interfaceMethodName)
    {
        INamedTypeSymbol? constructedInterface = typeArgument != null ? interfaceType?.Construct(typeArgument) : interfaceType;

        return constructedInterface?.GetMembers(interfaceMethodName).FirstOrDefault() is IMethodSymbol interfaceMethod
            && SymbolEqualityComparer.Default.Equals(method, method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod));
    }
}
