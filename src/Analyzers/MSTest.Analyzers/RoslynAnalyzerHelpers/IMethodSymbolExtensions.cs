// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;

using MSTest.Analyzers.Helpers;

namespace Analyzer.Utilities.Extensions;

internal static class IMethodSymbolExtensions
{
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
    /// Checks if the given method implements IDisposable.Dispose()
    /// </summary>
    public static bool IsDisposeImplementation(this IMethodSymbol method, Compilation compilation)
    {
        INamedTypeSymbol? iDisposable = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIDisposable);
        return method.IsDisposeImplementation(iDisposable);
    }

    /// <summary>
    /// Checks if the given method implements IAsyncDisposable.Dispose()
    /// </summary>
    public static bool IsAsyncDisposeImplementation(this IMethodSymbol method, Compilation compilation)
    {
        INamedTypeSymbol? iAsyncDisposable = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIAsyncDisposable);
        INamedTypeSymbol? valueTaskType = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadingTasksValueTask);
        return method.IsAsyncDisposeImplementation(iAsyncDisposable, valueTaskType);
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
        if (method == null)
        {
            return false;
        }

        if (method.IsOverride)
        {
            return method.OverriddenMethod.IsAsyncDisposeImplementation(iAsyncDisposable, valueTaskType);
        }

        // Identify the implementor of IAsyncDisposable.Dispose in the given method's containing type and check
        // if it is the given method.
        return SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskType) &&
               method.Parameters.IsEmpty &&
               method.IsImplementationOfInterfaceMethod(null, iAsyncDisposable, "DisposeAsync");
    }
}
