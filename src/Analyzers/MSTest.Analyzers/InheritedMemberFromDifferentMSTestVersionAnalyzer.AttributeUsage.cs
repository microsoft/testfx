// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MSTest.Analyzers;

public sealed partial class InheritedMemberFromDifferentMSTestVersionAnalyzer
{
    // Walks the applied attribute's base chain and returns the first ancestor that is a well-known MSTest attribute
    // (TestClass or one of the inheritance-sensitive lifecycle/test attributes).
    private static INamedTypeSymbol? GetCanonicalMSTestAttribute(INamedTypeSymbol? attributeClass)
    {
        for (INamedTypeSymbol? type = attributeClass; type is not null; type = type.BaseType)
        {
            if (type.ContainingNamespace is { } containingNamespace
                && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
                && IsCanonicalAttributeName(type.Name)
                && IsKnownFrameworkAssembly(type.ContainingAssembly))
            {
                return type;
            }
        }

        return null;
    }

    private static bool IsKnownFrameworkAssembly(IAssemblySymbol? assembly)
        => assembly is not null
            && (string.Equals(assembly.Name, LegacyFrameworkAssemblyName, StringComparison.Ordinal)
                || string.Equals(assembly.Name, CurrentFrameworkAssemblyName, StringComparison.Ordinal));

    // Reads InheritanceBehavior from the applied attribute's constructor arguments. When the legacy framework assembly
    // is entirely absent from the compilation the attribute constructor cannot bind and ConstructorArguments is empty,
    // so this returns false and the inherited class fixture is conservatively not reported. See the type-level remarks.
    private static bool HasBeforeEachDerivedClassBehavior(AttributeData attribute)
        => attribute.ConstructorArguments.Any(argument =>
            argument.Type is INamedTypeSymbol argumentType
            && string.Equals(argumentType.Name, InheritanceBehaviorTypeName, StringComparison.Ordinal)
            && argumentType.ContainingNamespace is { } containingNamespace
            && string.Equals(containingNamespace.ToDisplayString(), MSTestNamespace, StringComparison.Ordinal)
            && argument.Value is int value
            && value == BeforeEachDerivedClass);

    // The effective AttributeUsage.Inherited of the applied attribute. AttributeUsage is itself inherited down the
    // attribute class hierarchy, so the first [AttributeUsage] found walking up the base chain wins; when it is present
    // but omits Inherited the default is true. This is only called for an attribute that resolved to a canonical MSTest
    // attribute, and every standard MSTest test/lifecycle attribute declares Inherited=false — so when no [AttributeUsage]
    // is found at all (e.g. the legacy framework PE is absent and the attribute type cannot be read) the correct and
    // conservative fallback is false, matching the standard semantics rather than the generic .NET default of true.
    private static bool IsAppliedAttributeInheritable(AttributeData attribute)
    {
        for (INamedTypeSymbol? type = attribute.AttributeClass; type is not null; type = type.BaseType)
        {
            AttributeData? usage = type.GetAttributes().FirstOrDefault(applied =>
                applied.AttributeClass is { Name: "AttributeUsageAttribute" } usageClass
                && usageClass.ContainingNamespace is { } usageNamespace
                && string.Equals(usageNamespace.ToDisplayString(), "System", StringComparison.Ordinal));
            if (usage is null)
            {
                continue;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in usage.NamedArguments.Where(namedArgument => string.Equals(namedArgument.Key, "Inherited", StringComparison.Ordinal)))
            {
                if (namedArgument.Value.Value is bool inherited)
                {
                    return inherited;
                }
            }

            return true;
        }

        return false;
    }
}
