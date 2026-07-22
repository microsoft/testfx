// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Decides which attribute applications survive Native AOT trimming and converts the surviving
/// <see cref="AttributeData"/> into the emitter-facing <see cref="AttributeApplicationModel"/>. Attributes the
/// generated <c>new T(...)</c> could not reconstruct from the consuming assembly are dropped so the adapter
/// falls back to runtime reflection for them.
/// </summary>
internal static class AttributeMaterializationHelper
{
    internal readonly record struct AttributeMaterializationResult(
        EquatableArray<AttributeApplicationModel> Attributes,
        bool IsComplete);

    // Mirror the runtime behavior of MemberInfo.GetCustomAttributes(inherit: true): walk the
    // overridden-member chain, honor AttributeUsageAttribute.Inherited, and keep only the
    // most-derived application for attributes that do not allow multiple instances.
    internal static ImmutableArray<AttributeData> CollectInheritedAttributes(IMethodSymbol method)
    {
        ImmutableArray<AttributeData> own = method.GetAttributes();
        if (method.OverriddenMethod is null)
        {
            return own;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AttributeData>.Builder builder = ImmutableArray.CreateBuilder<AttributeData>();
        AppendAttributes(builder, seen, own, inheritedOnly: false);
        for (IMethodSymbol? baseMethod = method.OverriddenMethod; baseMethod is not null; baseMethod = baseMethod.OverriddenMethod)
        {
            AppendAttributes(builder, seen, baseMethod.GetAttributes(), inheritedOnly: true);
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<AttributeData> CollectInheritedAttributes(IPropertySymbol property)
    {
        ImmutableArray<AttributeData> own = property.GetAttributes();
        if (property.OverriddenProperty is null)
        {
            return own;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AttributeData>.Builder builder = ImmutableArray.CreateBuilder<AttributeData>();
        AppendAttributes(builder, seen, own, inheritedOnly: false);
        for (IPropertySymbol? baseProperty = property.OverriddenProperty; baseProperty is not null; baseProperty = baseProperty.OverriddenProperty)
        {
            AppendAttributes(builder, seen, baseProperty.GetAttributes(), inheritedOnly: true);
        }

        return builder.ToImmutable();
    }

    internal static ImmutableArray<AttributeData> CollectInheritedAttributes(INamedTypeSymbol type)
    {
        ImmutableArray<AttributeData> own = type.GetAttributes();
        if (type.BaseType is null || type.BaseType.SpecialType == SpecialType.System_Object)
        {
            return own;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AttributeData>.Builder builder = ImmutableArray.CreateBuilder<AttributeData>();
        AppendAttributes(builder, seen, own, inheritedOnly: false);
        for (INamedTypeSymbol? baseType = type.BaseType;
             baseType is not null && baseType.SpecialType != SpecialType.System_Object;
             baseType = baseType.BaseType)
        {
            AppendAttributes(builder, seen, baseType.GetAttributes(), inheritedOnly: true);
        }

        return builder.ToImmutable();
    }

    private static void AppendAttributes(
        ImmutableArray<AttributeData>.Builder builder,
        HashSet<string> seen,
        ImmutableArray<AttributeData> attributes,
        bool inheritedOnly)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            AttributeUsageMetadata usage = GetAttributeUsage(attributeClass);
            if (inheritedOnly && !usage.Inherited)
            {
                continue;
            }

            string key = attributeClass.ToDisplayString(SymbolDisplayFormats.FullyQualified);
            if (usage.AllowMultiple || seen.Add(key))
            {
                builder.Add(attribute);
            }
        }
    }

    private static AttributeUsageMetadata GetAttributeUsage(INamedTypeSymbol attributeClass)
    {
        bool inherited = true;
        bool allowMultiple = false;

        // [AttributeUsage] is itself inherited (its own AttributeUsage declares Inherited=true).
        // Roslyn's GetAttributes() does NOT walk the base-type chain, so we have to walk it
        // ourselves to honor an [AttributeUsage] declared on a base attribute type (e.g. when
        // a user-defined attribute derives from one of MSTest's attributes without re-declaring
        // its own [AttributeUsage]).
        for (INamedTypeSymbol? current = attributeClass;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            if (TryReadAttributeUsage(current, out bool currentInherited, out bool currentAllowMultiple))
            {
                inherited = currentInherited;
                allowMultiple = currentAllowMultiple;
                break;
            }
        }

        return new AttributeUsageMetadata(inherited, allowMultiple);
    }

    private static bool TryReadAttributeUsage(INamedTypeSymbol attributeClass, out bool inherited, out bool allowMultiple)
    {
        inherited = true;
        allowMultiple = false;

        foreach (AttributeData attribute in attributeClass.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormats.FullyQualified) != "global::System.AttributeUsageAttribute")
            {
                continue;
            }

            foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments)
            {
                if (namedArgument.Value.Value is not bool value)
                {
                    continue;
                }

                switch (namedArgument.Key)
                {
                    case nameof(AttributeUsageAttribute.Inherited):
                        inherited = value;
                        break;
                    case nameof(AttributeUsageAttribute.AllowMultiple):
                        allowMultiple = value;
                        break;
                }
            }

            return true;
        }

        return false;
    }

    private readonly record struct AttributeUsageMetadata(bool Inherited, bool AllowMultiple);

    public static EquatableArray<AttributeApplicationModel> BuildAttributes(
        ImmutableArray<AttributeData> attributes,
        IAssemblySymbol consumingAssembly)
        => BuildAttributesWithCompleteness(attributes, consumingAssembly).Attributes;

    internal static AttributeMaterializationResult BuildAttributesWithCompleteness(
        ImmutableArray<AttributeData> attributes,
        IAssemblySymbol consumingAssembly)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return new(EquatableArray<AttributeApplicationModel>.Empty, IsComplete: true);
        }

        ImmutableArray<AttributeApplicationModel>.Builder materialized = ImmutableArray.CreateBuilder<AttributeApplicationModel>();
        bool isComplete = true;
        foreach (AttributeData attribute in attributes)
        {
            if (BuildAttribute(attribute, consumingAssembly) is { } application)
            {
                materialized.Add(application);
            }
            else
            {
                isComplete = false;
            }
        }

        return new(new EquatableArray<AttributeApplicationModel>(materialized.ToImmutable()), isComplete);
    }

    private static AttributeApplicationModel? BuildAttribute(AttributeData attribute, IAssemblySymbol consumingAssembly)
    {
        if (attribute.AttributeClass is not { } attributeClass)
        {
            return null;
        }

        // Safener: only materialize attributes the generated code can actually reconstruct with
        // `new T(...)`. Anything that would not compile from the consuming assembly (inaccessible
        // attribute type or constructor, or an argument referencing an inaccessible type) is omitted
        // so the adapter falls back to runtime reflection for it. Omission is always safe; emitting
        // an un-compilable expression would break the build.
        if (!IsAttributeMaterializable(attribute, attributeClass, consumingAssembly))
        {
            return null;
        }

        IEnumerable<TypedConstantModel> ctorArgs = attribute.ConstructorArguments.Select(ToModel);
        IEnumerable<NamedArgumentModel> namedArgs = attribute.NamedArguments.Select(static kv => new NamedArgumentModel(kv.Key, ToModel(kv.Value)));

        return new AttributeApplicationModel(
            FullyQualifiedAttributeType: attributeClass.ToDisplayString(SymbolDisplayFormats.FullyQualified),
            ConstructorArguments: ctorArgs.ToEquatableArray(),
            NamedArguments: namedArgs.ToEquatableArray());
    }

    private static bool IsAttributeMaterializable(AttributeData attribute, INamedTypeSymbol attributeClass, IAssemblySymbol consumingAssembly)
        // The attribute type (and every enclosing type) must be referenceable; the constructor the
        // generated `new T(...)` binds to must be callable (a null AttributeConstructor — Roslyn could
        // not resolve it — is treated as not materializable); and every argument type the emitter
        // writes out (enum casts, typeof targets, typed nulls, nested array elements) must also be
        // referenceable.
        => SymbolReferenceabilityHelper.IsTypeReferenceableFrom(attributeClass, consumingAssembly)
            && attribute.AttributeConstructor is { } constructor
            && SymbolReferenceabilityHelper.IsMemberAccessibleFrom(constructor.DeclaredAccessibility, constructor.ContainingType, consumingAssembly)
            && attribute.ConstructorArguments.All(argument => AreArgumentTypesReferenceable(argument, consumingAssembly))
            && attribute.NamedArguments.All(named => AreArgumentTypesReferenceable(named.Value, consumingAssembly));

    private static bool AreArgumentTypesReferenceable(TypedConstant constant, IAssemblySymbol consumingAssembly)
        => constant.Kind switch
        {
            TypedConstantKind.Array => constant.Values.All(element => AreArgumentTypesReferenceable(element, consumingAssembly)),

            // typeof(X): the target type must be referenceable. Non-named targets (arrays, type
            // parameters) are conservatively rejected.
            TypedConstantKind.Type => constant.Value is null
                || (constant.Value is INamedTypeSymbol typeofTarget && SymbolReferenceabilityHelper.IsTypeReferenceableFrom(typeofTarget, consumingAssembly)),

            // Enum casts and typed nulls emit a `(Type)` cast, so the constant's declared type must be
            // referenceable. Untyped values (Type is null) are plain literals.
            _ => constant.Type is not INamedTypeSymbol namedType
                || SymbolReferenceabilityHelper.IsTypeReferenceableFrom(namedType, consumingAssembly),
        };

    internal static TypedConstantModel ToModel(TypedConstant constant)
        => constant switch
        {
            { IsNull: true } => new TypedConstantModel(
                ConstantValueKind.Null,
                constant.Type?.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                null,
                EquatableArray<TypedConstantModel>.Empty),
            { Kind: TypedConstantKind.Array } => new TypedConstantModel(
                ConstantValueKind.Array,
                constant.Type?.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                null,
                constant.Values.Select(ToModel).ToEquatableArray()),
            { Kind: TypedConstantKind.Enum } => new TypedConstantModel(
                ConstantValueKind.Enum,
                constant.Type?.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                constant.Value,
                EquatableArray<TypedConstantModel>.Empty),
            { Kind: TypedConstantKind.Type } => new TypedConstantModel(
                ConstantValueKind.Type,
                (constant.Value as ITypeSymbol)?.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                null,
                EquatableArray<TypedConstantModel>.Empty),
            _ => new TypedConstantModel(
                ConstantValueKind.Primitive,
                constant.Type?.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                constant.Value,
                EquatableArray<TypedConstantModel>.Empty),
        };
}
