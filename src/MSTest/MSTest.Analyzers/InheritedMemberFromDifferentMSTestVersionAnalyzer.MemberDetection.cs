// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class InheritedMemberFromDifferentMSTestVersionAnalyzer
{
    private static bool IsCanonicalAttributeName(string name)
        => string.Equals(name, TestClassAttributeName, StringComparison.Ordinal)
        || GetMemberKind(name) != MSTestMemberKind.None;

    private static MSTestMemberKind GetMemberKind(string canonicalAttributeName) => canonicalAttributeName switch
    {
        "TestMethodAttribute" or "DataTestMethodAttribute" => MSTestMemberKind.TestMethod,
        "TestInitializeAttribute" or "TestCleanupAttribute" => MSTestMemberKind.InstanceFixture,
        "ClassInitializeAttribute" => MSTestMemberKind.ClassInitialize,
        "ClassCleanupAttribute" => MSTestMemberKind.ClassCleanup,

        // AssemblyInitialize/AssemblyCleanup are intentionally excluded: they are discovered per-assembly, not
        // inherited, so a base library's assembly fixture never runs on a derived test class even after recompilation.
        _ => MSTestMemberKind.None,
    };

    private static bool WouldRunOrBeDiscoveredIfSameVersion(IMethodSymbol method, MSTestMemberKind kind, AttributeData attribute, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType, IAssemblySymbol referenceAssembly, bool canDiscoverInternals, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        // A non-public member, or one replaced by an override or a `new` declaration in a more-derived type, is not the
        // member MSTest would run or discover, so recompiling the base cannot change its behavior.
        => method.DeclaredAccessibility == Accessibility.Public
            && !IsHiddenOrOverriddenInDerivedType(method, kind, attribute, testClass, declaringBaseType, referenceAssembly, canDiscoverInternals, taskSymbol, valueTaskSymbol)
            && kind switch
            {
                // Test methods must be public instance methods that return void/Task/ValueTask (parameters are allowed
                // for data-driven tests). Generic method definitions are discovered even when type inference later
                // fails during execution.
                MSTestMemberKind.TestMethod
                    => !method.IsStatic
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol),

                // Instance fixtures must be public, non-static, non-generic, parameterless, and return void/Task/ValueTask.
                MSTestMemberKind.InstanceFixture
                    => !method.IsStatic && !method.IsGenericMethod && method.Parameters.IsEmpty && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol),

                // ClassInitialize must be a public static non-generic method taking exactly one TestContext, returning
                // void/Task/ValueTask, and only flows to derived classes when BeforeEachDerivedClass is set.
                MSTestMemberKind.ClassInitialize
                    => method.IsStatic
                        && !method.IsGenericMethod
                        && HasSingleTestContextParameter(method)
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
                        && HasBeforeEachDerivedClassBehavior(attribute),

                // ClassCleanup is like ClassInitialize but its TestContext parameter is optional.
                MSTestMemberKind.ClassCleanup
                    => method.IsStatic
                        && !method.IsGenericMethod
                        && HasOptionalTestContextParameter(method)
                        && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
                        && HasBeforeEachDerivedClassBehavior(attribute),

                _ => false,
            };

    // A fixture/test method must return void (non-async), non-generic Task, or non-generic ValueTask. Comparing against
    // the resolved Task/ValueTask symbols rejects Task&lt;T&gt;/ValueTask&lt;T&gt;, and the async check rejects async void.
    private static bool ReturnsVoidTaskOrValueTask(IMethodSymbol method, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        => method.ReturnsVoid
            ? !method.IsAsync
            : SymbolEqualityComparer.Default.Equals(method.ReturnType, taskSymbol)
                || SymbolEqualityComparer.Default.Equals(method.ReturnType, valueTaskSymbol);

    private static bool HasSingleTestContextParameter(IMethodSymbol method)
        => method.Parameters.Length == 1 && IsTestContext(method.Parameters[0].Type);

    private static bool HasOptionalTestContextParameter(IMethodSymbol method)
        => method.Parameters.IsEmpty || HasSingleTestContextParameter(method);

    private static bool IsTestContext(ITypeSymbol type)
        => type.ContainingNamespace is { } containingNamespace
            && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
            && string.Equals(type.Name, "TestContext", StringComparison.Ordinal)
            && IsKnownTestContextAssembly(type.ContainingAssembly);

    private static bool IsKnownTestContextAssembly(IAssemblySymbol? assembly)
        => assembly is not null
            && (IsKnownFrameworkAssembly(assembly)
                || string.Equals(assembly.Name, LegacyFrameworkExtensionsAssemblyName, StringComparison.Ordinal)
                || string.Equals(assembly.Name, CurrentFrameworkExtensionsAssemblyName, StringComparison.Ordinal));

    private static bool IsHiddenOrOverriddenInDerivedType(IMethodSymbol baseMethod, MSTestMemberKind kind, AttributeData attribute, INamedTypeSymbol testClass, INamedTypeSymbol declaringBaseType, IAssemblySymbol referenceAssembly, bool canDiscoverInternals, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
    {
        // Class fixtures are static and accumulated independently by the adapter, so a same-named derived method does
        // not suppress them (and static methods cannot be overridden anyway).
        if (kind is MSTestMemberKind.ClassInitialize or MSTestMemberKind.ClassCleanup)
        {
            return false;
        }

        for (INamedTypeSymbol? type = testClass;
            type is not null && !SymbolEqualityComparer.Default.Equals(type, declaringBaseType);
            type = type.BaseType)
        {
            foreach (ISymbol member in type.GetMembers(baseMethod.Name))
            {
                if (member is not IMethodSymbol derivedMethod)
                {
                    continue;
                }

                // An override normally replaces the inherited member: standard test/lifecycle attributes use
                // Inherited=false, so an attribute-less override does not carry the attribute. But a custom attribute
                // with Inherited=true flows onto the override via reflection (the adapter reads attributes with
                // inherit:true); when it derives from a different-version framework type, the override is discoverable
                // only after recompilation, so it is a real break that must still be reported — unless the override
                // carries its own current-version attribute of the same kind (then it is discoverable regardless).
                // The walk is top-down, so the first override found is the most-derived one that reflection surfaces;
                // it is the effective method, so its outcome is decisive and we return immediately either way.
                if (Overrides(derivedMethod, baseMethod))
                {
                    return !IsAppliedAttributeInheritable(attribute)
                        || HasOwnCurrentVersionAttributeOfKind(derivedMethod, kind, referenceAssembly);
                }

                switch (kind)
                {
                    // An inherited instance fixture is only suppressed when the hiding method is itself a valid
                    // instance fixture; a private/static/parameterized same-name method leaves the base one running.
                    case MSTestMemberKind.InstanceFixture
                        when derivedMethod is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }
                            && derivedMethod.Parameters.IsEmpty
                            && ReturnsVoidTaskOrValueTask(derivedMethod, taskSymbol, valueTaskSymbol):
                        return true;

                    // A derived same-name method shadows the inherited test in MSTest discovery only when it is itself
                    // a valid, current-version test method. TypeEnumerator enumerates every runtime method, keeps only
                    // those that pass IsValidTestMethod, and de-duplicates by MethodInfo.ToString() (which includes the
                    // return type). A derived method with a different return type, a static one, or one without a
                    // recognized current-version [TestMethod] is skipped, so the inherited base test is still discovered.
                    case MSTestMemberKind.TestMethod
                        when IsDiscoverableTestMethod(derivedMethod, referenceAssembly, canDiscoverInternals, taskSymbol, valueTaskSymbol)
                            && HaveSameSignature(derivedMethod, baseMethod):
                        return true;
                }
            }
        }

        return false;
    }

    private static bool HasOwnCurrentVersionAttributeOfKind(IMethodSymbol method, MSTestMemberKind kind, IAssemblySymbol referenceAssembly)
        => method.GetAttributes().Any(attribute =>
            GetCanonicalMSTestAttribute(attribute.AttributeClass) is { } canonicalAttribute
            && GetMemberKind(canonicalAttribute.Name) == kind
            && canonicalAttribute.ContainingAssembly is { } assembly
            && string.Equals(assembly.Name, referenceAssembly.Name, StringComparison.Ordinal));

    private static bool Overrides(IMethodSymbol candidate, IMethodSymbol baseMethod)
    {
        for (IMethodSymbol? overridden = candidate.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, baseMethod.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    // Mirrors the adapter's IsValidTestMethod for the purpose of discovery de-duplication: a method only shadows an
    // inherited test when it is itself discoverable — instance, discoverable accessibility (public, or non-private when
    // the assembly opts into DiscoverInternals), returns void/Task/ValueTask, and carries a
    // [TestMethod]/[DataTestMethod] from the current framework (a legacy-version attribute is not recognized by the
    // current adapter, so it would not be discovered either).
    private static bool IsDiscoverableTestMethod(IMethodSymbol method, IAssemblySymbol referenceAssembly, bool canDiscoverInternals, INamedTypeSymbol? taskSymbol, INamedTypeSymbol? valueTaskSymbol)
        => !method.IsStatic
            && HasDiscoverableTestMethodAccessibility(method, canDiscoverInternals)
            && ReturnsVoidTaskOrValueTask(method, taskSymbol, valueTaskSymbol)
            && method.GetAttributes().Any(attribute =>
                GetCanonicalMSTestAttribute(attribute.AttributeClass) is { } canonicalAttribute
                && GetMemberKind(canonicalAttribute.Name) == MSTestMemberKind.TestMethod
                && canonicalAttribute.ContainingAssembly is { } assembly
                && string.Equals(assembly.Name, referenceAssembly.Name, StringComparison.Ordinal));

    private static bool HasDiscoverableTestMethodAccessibility(IMethodSymbol method, bool canDiscoverInternals)
        // Mirror TestMethodValidator: a discoverable test method is exactly public, or exactly internal when the
        // assembly opts into DiscoverInternals. Protected/protected-internal/private-protected/private methods are never
        // discovered, so a derived one cannot hide the inherited test.
        => method.DeclaredAccessibility == Accessibility.Public
            || (canDiscoverInternals && method.DeclaredAccessibility == Accessibility.Internal);

    private static bool HaveSameSignature(IMethodSymbol left, IMethodSymbol right)
    {
        // MSTest discovery de-duplicates by MethodInfo.ToString(), which includes the return type, and the
        // static/instance calling convention also distinguishes the CLR method, so both must match for one method to
        // shadow the other.
        if (left.Arity != right.Arity
            || left.Parameters.Length != right.Parameters.Length
            || left.IsStatic != right.IsStatic
            || !SymbolEqualityComparer.Default.Equals(left.ReturnType, right.ReturnType))
        {
            return false;
        }

        for (int index = 0; index < left.Parameters.Length; index++)
        {
            IParameterSymbol leftParameter = left.Parameters[index];
            IParameterSymbol rightParameter = right.Parameters[index];

            // ref/out/in all share the same by-ref CLR signature, so compare by-value versus by-ref rather than the
            // exact RefKind (a derived 'Run(out int)' still hides a base 'Run(ref int)').
            if ((leftParameter.RefKind == RefKind.None) != (rightParameter.RefKind == RefKind.None)
                || !AreSignatureTypesEquivalent(leftParameter.Type, rightParameter.Type))
            {
                return false;
            }
        }

        return true;
    }

    // Compares parameter types the way MSTest discovery de-duplicates methods — by MethodInfo.ToString(), which prints
    // a method type parameter by its name (e.g. 'Void Run[T](T)'). So 'Run&lt;T&gt;(T)' and 'Run&lt;U&gt;(U)' have
    // different discovery keys and do not hide each other, while 'Run&lt;T&gt;(T)' hides 'Run&lt;T&gt;(T)'. Recurses
    // through arrays and constructed generic types and falls back to symbol identity for everything else.
    private static bool AreSignatureTypesEquivalent(ITypeSymbol left, ITypeSymbol right)
    {
        if (left is ITypeParameterSymbol leftTypeParameter && right is ITypeParameterSymbol rightTypeParameter)
        {
            return leftTypeParameter.TypeParameterKind == rightTypeParameter.TypeParameterKind
                && string.Equals(leftTypeParameter.Name, rightTypeParameter.Name, StringComparison.Ordinal);
        }

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
        {
            return leftArray.Rank == rightArray.Rank
                && AreSignatureTypesEquivalent(leftArray.ElementType, rightArray.ElementType);
        }

        if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed && !leftNamed.TypeArguments.IsEmpty)
        {
            if (leftNamed.TypeArguments.Length != rightNamed.TypeArguments.Length
                || !SymbolEqualityComparer.Default.Equals(leftNamed.OriginalDefinition, rightNamed.OriginalDefinition))
            {
                return false;
            }

            for (int index = 0; index < leftNamed.TypeArguments.Length; index++)
            {
                if (!AreSignatureTypesEquivalent(leftNamed.TypeArguments[index], rightNamed.TypeArguments[index]))
                {
                    return false;
                }
            }

            return true;
        }

        return SymbolEqualityComparer.Default.Equals(left, right);
    }

    private static string GetDisplayName(INamedTypeSymbol attributeClass)
    {
        const string attributeSuffix = "Attribute";
        string name = attributeClass.Name;
        return name.EndsWith(attributeSuffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - attributeSuffix.Length)
            : name;
    }

    private enum MSTestMemberKind
    {
        None,
        TestMethod,
        InstanceFixture,
        ClassInitialize,
        ClassCleanup,
    }
}
