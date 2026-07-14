// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Reusable predicates for deciding whether a Roslyn symbol can be referenced from the generated
/// registration code (which lives in the consuming assembly). Used to keep the emitter from producing
/// expressions that reference inaccessible or open (unbound) types.
/// </summary>
internal static class SymbolReferenceabilityHelper
{
    // A type is safe to emit (as typeof(...) and as a member-access receiver) only when it is referenceable
    // from the consuming assembly and fully closed (no open type parameters).
    internal static bool IsClosedReferenceableType(INamedTypeSymbol type, IAssemblySymbol consumingAssembly)
        => !ContainsTypeParameter(type) && IsTypeReferenceableFrom(type, consumingAssembly);

    internal static bool ContainsTypeParameter(INamedTypeSymbol type)
        => type.IsUnboundGenericType
            || type.TypeArguments.Any(static argument =>
                argument is ITypeParameterSymbol
                || (argument is INamedTypeSymbol named && ContainsTypeParameter(named)));

    internal static bool IsTypeReferenceableFrom(INamedTypeSymbol type, IAssemblySymbol consumingAssembly)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            if (!IsMemberAccessibleFrom(current.DeclaredAccessibility, current.ContainingAssembly, consumingAssembly))
            {
                return false;
            }
        }

        // Also require every generic type argument to be referenceable (e.g. a closed generic
        // attribute type argument that is itself inaccessible).
        return type.TypeArguments
            .OfType<INamedTypeSymbol>()
            .All(namedArgument => IsTypeReferenceableFrom(namedArgument, consumingAssembly));
    }

    internal static bool IsMemberAccessibleFrom(Accessibility accessibility, INamedTypeSymbol containingType, IAssemblySymbol consumingAssembly)
        => IsMemberAccessibleFrom(accessibility, containingType.ContainingAssembly, consumingAssembly);

    internal static bool IsMemberAccessibleFrom(Accessibility accessibility, IAssemblySymbol? declaringAssembly, IAssemblySymbol consumingAssembly)
        => accessibility switch
        {
            Accessibility.Public => true,

            // Generated code lives in the consuming assembly, so internal / protected-internal members
            // are reachable only when declared in that same assembly (we do not rely on InternalsVisibleTo).
            Accessibility.Internal or Accessibility.ProtectedOrInternal =>
                declaringAssembly is not null && SymbolEqualityComparer.Default.Equals(declaringAssembly, consumingAssembly),

            // NotApplicable shows up for compiler-synthesized symbols in well-formed source; treat as reachable.
            Accessibility.NotApplicable => true,

            // Private, Protected, and ProtectedAndInternal ("private protected") are never reachable
            // from the generated (non-derived) call site.
            _ => false,
        };
}
