// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Extensions;

internal static class IMethodSymbolExtensions
{
    public static bool IsImplementationOfAnyInterfaceMember(this ISymbol symbol)
        => IsImplementationOfAnyInterfaceMember<ISymbol>(symbol);

    /// <summary>
    /// Checks if a given symbol implements an interface member implicitly
    /// </summary>
    public static bool IsImplementationOfAnyInterfaceMember<TSymbol>(this ISymbol symbol)
        where TSymbol : ISymbol
    {
        if (symbol.ContainingType == null)
        {
            return false;
        }

        foreach (INamedTypeSymbol interfaceSymbol in symbol.ContainingType.AllInterfaces)
        {
            foreach (TSymbol interfaceMember in interfaceSymbol.GetMembers().OfType<TSymbol>())
            {
                if (IsImplementationOfInterfaceMember(symbol, interfaceMember))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsImplementationOfInterfaceMember(this ISymbol symbol, [NotNullWhen(returnValue: true)] ISymbol? interfaceMember)
        => interfaceMember != null
        && SymbolEqualityComparer.Default.Equals(symbol, symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMember));

    /// <summary>
    /// Checks if the given method is an implementation of the given interface method
    /// Substituted with the given typeargument.
    /// </summary>
    public static bool IsImplementationOfInterfaceMethod(this IMethodSymbol method, ITypeSymbol? typeArgument, [NotNullWhen(returnValue: true)] INamedTypeSymbol? interfaceType, string interfaceMethodName)
    {
        INamedTypeSymbol? constructedInterface = typeArgument != null ? interfaceType?.Construct(typeArgument) : interfaceType;

        return constructedInterface?.GetMembers(interfaceMethodName).FirstOrDefault() is IMethodSymbol interfaceMethod &&
            SymbolEqualityComparer.Default.Equals(method, method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod));
    }

    /// <summary>
    /// Checks if the given method implements <see cref="IDisposable.Dispose"/> or overrides an implementation of <see cref="IDisposable.Dispose"/>.
    /// </summary>
    public static bool IsDisposeImplementation([NotNullWhen(returnValue: true)] this IMethodSymbol? method, [NotNullWhen(returnValue: true)] INamedTypeSymbol? iDisposable)
    {
        if (method == null)
        {
            return false;
        }

        if (method.IsOverride)
        {
            return method.OverriddenMethod.IsDisposeImplementation(iDisposable);
        }

        // Identify the implementor of IDisposable.Dispose in the given method's containing type and check
        // if it is the given method.
        return method.ReturnsVoid &&
            method.Parameters.IsEmpty &&
            method.IsImplementationOfInterfaceMethod(null, iDisposable, "Dispose");
    }

    /// <summary>
    /// Checks if the given method implements "IAsyncDisposable.Dispose" or overrides an implementation of "IAsyncDisposable.Dispose".
    /// </summary>
    public static bool IsAsyncDisposeImplementation([NotNullWhen(returnValue: true)] this IMethodSymbol? method, [NotNullWhen(returnValue: true)] INamedTypeSymbol? iAsyncDisposable, [NotNullWhen(returnValue: true)] INamedTypeSymbol? valueTaskType)
    {
        while (true)
        {
            if (method == null)
            {
                return false;
            }

            if (method.IsOverride)
            {
                method = method.OverriddenMethod;
                continue;
            }

            // Identify the implementor of IAsyncDisposable.Dispose in the given method's containing type and check
            // if it is the given method.
            return SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskType) &&
                   method.Parameters.IsEmpty &&
                   method.IsImplementationOfInterfaceMethod(null, iAsyncDisposable, "DisposeAsync");
        }
    }
}
