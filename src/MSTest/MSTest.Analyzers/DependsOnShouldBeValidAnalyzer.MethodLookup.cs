// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using MSTest.Analyzers.Helpers;

namespace MSTest.Analyzers;

public sealed partial class DependsOnShouldBeValidAnalyzer
{
    /// <summary>
    /// Whether the type's hierarchy declares the same method signature twice <em>among the methods discovery
    /// turns into tests</em>, which is what makes <c>TypeEnumerator.GetTests</c> fall back to collapsing tests
    /// by name. Methods discovery never looks at - private helpers and the like - cannot trigger it, so they
    /// must not disable cycle analysis here either.
    /// </summary>
    private static bool HasDuplicateSignature(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers())
            {
                // An override is not a duplicate: reflection surfaces only the most-derived declaration.
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, OverriddenMethod: null } method
                    && IsRunnableTestMethod(method, symbols)
                    && !signatures.Add(BuildSignatureKey(method)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static MethodLookup LookupMethods(INamedTypeSymbol targetClass, string methodName, AnalysisSymbols symbols)
    {
        bool exists = false;
        foreach (IMethodSymbol method in EnumerateMethods(targetClass, methodName, symbols))
        {
            exists = true;

            // The name is what the run-time graph matches on, so any overload in the type's effective method
            // set being a test is enough for the reference to resolve.
            if (method.IsTestMethod(symbols.TestMethodAttribute))
            {
                return new MethodLookup(Exists: true, IsTestMethod: true);
            }
        }

        return new MethodLookup(exists, IsTestMethod: false);
    }

    /// <summary>
    /// Enumerates the methods named <paramref name="methodName"/> that <paramref name="type"/> effectively
    /// has: its own declarations plus the inherited ones, excluding base declarations that an override
    /// replaces.
    /// </summary>
    private static IEnumerable<IMethodSymbol> EnumerateMethods(INamedTypeSymbol type, string methodName, AnalysisSymbols symbols)
        => EnumerateEffectiveMethods(type, methodName, symbols);

    private static bool HasRunnableTestMethod(INamedTypeSymbol targetClass, string methodName, AnalysisSymbols symbols)
    {
        foreach (IMethodSymbol method in EnumerateMethods(targetClass, methodName, symbols))
        {
            if (IsRunnableTestMethod(method, symbols))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateTestMethodNames(INamedTypeSymbol type, AnalysisSymbols symbols)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IMethodSymbol method in EnumerateEffectiveMethods(type, methodName: null, symbols))
        {
            if (IsRunnableTestMethod(method, symbols) && seen.Add(method.Name))
            {
                yield return method.Name;
            }
        }
    }

    /// <summary>
    /// Walks <paramref name="type"/> and its base types most-derived first, yielding the ordinary methods
    /// that make up the type's effective method set - optionally only those named
    /// <paramref name="methodName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A base declaration that an override replaces is dropped, because reflection surfaces only the
    /// most-derived declaration and both <c>[TestMethod]</c> and <c>[DependsOn]</c> are declared
    /// <c>Inherited = false</c>. Keeping the base declaration would let its attributes travel to the
    /// override: an overridden test whose override does not re-declare <c>[TestMethod]</c> would still count
    /// as a test, and - worse - a <c>[DependsOn]</c> the author deliberately dropped by overriding would
    /// still contribute an edge, which can close a cycle that does not exist at run time.
    /// </para>
    /// <para>
    /// A base declaration that a <c>new</c> member hides with the <em>same signature</em> is dropped for the
    /// same reason: <c>TypeEnumerator.GetTests</c> detects the duplicate and keeps the declaration closest to
    /// the test class. Genuine overloads (a different signature) are kept, since those are distinct tests.
    /// That duplicate detection only looks at the methods discovery turns into tests, so a hiding declaration
    /// discovery ignores - a private helper, say - must not displace the base test it happens to shadow.
    /// </para>
    /// </remarks>
    private static IEnumerable<IMethodSymbol> EnumerateEffectiveMethods(INamedTypeSymbol type, string? methodName, AnalysisSymbols symbols)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var signatures = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (ISymbol member in methodName is null ? current.GetMembers() : current.GetMembers(methodName))
            {
                if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                    || overridden.Contains(method))
                {
                    continue;
                }

                if (IsRunnableTestMethod(method, symbols) && !signatures.Add(BuildSignatureKey(method)))
                {
                    continue;
                }

                for (IMethodSymbol? baseMethod = method.OverriddenMethod; baseMethod is not null; baseMethod = baseMethod.OverriddenMethod)
                {
                    overridden.Add(baseMethod);
                }

                yield return method;
            }
        }
    }

    /// <summary>
    /// Builds a key identifying a method's CLR signature. Method type parameters are normalized to their
    /// ordinal, and the walk is structural, because two declarations that hide each other can spell their
    /// type parameters differently (<c>Run&lt;T&gt;(T)</c> and <c>Run&lt;U&gt;(U)</c> are the same signature).
    /// Rendering the parameter types as source text would miss that and keep the hidden declaration.
    /// </summary>
    /// <remarks>
    /// Erring toward collision is safe here: the walk yields most-derived first, so a key collision only ever
    /// drops a <em>base</em> declaration, which removes edges rather than inventing them.
    /// </remarks>
    private static string BuildSignatureKey(IMethodSymbol method)
    {
        var builder = new StringBuilder(method.Name);
        builder.Append('`').Append(method.Arity).Append('(');
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            IParameterSymbol parameter = method.Parameters[i];
            builder.Append(parameter.RefKind).Append(':');
            AppendTypeKey(builder, parameter.Type);
        }

        return builder.Append(')').ToString();
    }

    private static void AppendTypeKey(StringBuilder builder, ITypeSymbol type)
    {
        switch (type)
        {
            // The name a method type parameter is given is not part of the signature; its position is.
            case ITypeParameterSymbol { TypeParameterKind: TypeParameterKind.Method } methodTypeParameter:
                builder.Append("!!").Append(methodTypeParameter.Ordinal);
                return;

            // 'dynamic' and 'object' are the same type to the CLR.
            case IDynamicTypeSymbol:
                builder.Append("object");
                return;

            case IArrayTypeSymbol array:
                AppendTypeKey(builder, array.ElementType);
                builder.Append('[').Append(array.Rank).Append(']');
                return;

            case IPointerTypeSymbol pointer:
                AppendTypeKey(builder, pointer.PointedAtType);
                builder.Append('*');
                return;

            // A constructed generic has to be compared argument by argument, so that 'List<T>' and 'List<U>'
            // are recognized as the same signature.
            case INamedTypeSymbol { IsGenericType: true } named:
                builder.Append(named.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('<');
                for (int i = 0; i < named.TypeArguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    AppendTypeKey(builder, named.TypeArguments[i]);
                }

                builder.Append('>');
                return;

            default:
                builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                return;
        }
    }

    private readonly record struct MethodLookup(bool Exists, bool IsTestMethod);
}
