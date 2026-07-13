// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Resolves each <c>[DynamicData(...)]</c> on a test method to a concrete source member (and optional custom
/// display-name method) at compile time so the generator can register a reflection-free accessor with
/// DynamicDataSourceResolver. Sources the generator cannot resolve (missing member, inaccessible, wrong
/// shape) are skipped: at runtime DynamicDataOperations falls back to reflection for those.
/// </summary>
internal static class DynamicDataSourceBuilder
{
    // Mirrors Microsoft.VisualStudio.TestTools.UnitTesting.DynamicDataSourceType (which lives in the test
    // framework and is not referenceable from this analyzer). Values must match the runtime enum.
    private enum DynamicDataSourceType
    {
        Property = 0,
        Method = 1,
        AutoDetect = 2,
        Field = 3,
    }

    internal static EquatableArray<DynamicDataSourceModel> BuildDynamicDataSources(ImmutableArray<AttributeData> attributes, IMethodSymbol testMethod, IAssemblySymbol consumingAssembly)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return EquatableArray<DynamicDataSourceModel>.Empty;
        }

        ImmutableArray<DynamicDataSourceModel>.Builder? builder = null;
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormats.FullyQualified) != "global::" + MSTestAttributeNames.DynamicData)
            {
                continue;
            }

            if (TryBuildDynamicDataSource(attribute, testMethod, consumingAssembly) is { } model)
            {
                (builder ??= ImmutableArray.CreateBuilder<DynamicDataSourceModel>()).Add(model);
            }
        }

        return builder is null
            ? EquatableArray<DynamicDataSourceModel>.Empty
            : new EquatableArray<DynamicDataSourceModel>(builder.ToImmutable());
    }

    private static DynamicDataSourceModel? TryBuildDynamicDataSource(AttributeData attribute, IMethodSymbol testMethod, IAssemblySymbol consumingAssembly)
    {
        ImmutableArray<TypedConstant> ctorArgs = attribute.ConstructorArguments;
        if (ctorArgs.IsDefaultOrEmpty || ctorArgs[0].Value is not string sourceName)
        {
            return null;
        }

        // The declaring type defaults to the type that declares the test method, matching
        // methodInfo.DeclaringType at runtime (so inherited [DynamicData] resolves under the base type, not
        // the leaf). An explicit typeof(...) constructor argument overrides it, and an explicit
        // DynamicDataSourceType argument constrains member resolution. The params object[] arguments do not
        // affect resolution.
        INamedTypeSymbol declaringType = testMethod.ContainingType;
        DynamicDataSourceType sourceType = DynamicDataSourceType.AutoDetect;
        for (int i = 1; i < ctorArgs.Length; i++)
        {
            TypedConstant arg = ctorArgs[i];
            if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol explicitType)
            {
                declaringType = explicitType;
            }
            else if (arg.Kind == TypedConstantKind.Enum
                && arg.Type?.ToDisplayString(SymbolDisplayFormats.FullyQualified) == "global::" + MSTestAttributeNames.DynamicDataSourceType
                && arg.Value is int sourceTypeValue)
            {
                // An attribute can carry an undefined cast enum value, e.g. (DynamicDataSourceType)99, or a
                // value from a newer framework than this generator understands. Emitting
                // DynamicDataSourceType.99 would not compile, so bail out and let the runtime fallback handle it.
                if (!Enum.IsDefined(typeof(DynamicDataSourceType), sourceTypeValue))
                {
                    return null;
                }

                sourceType = (DynamicDataSourceType)sourceTypeValue;
            }
        }

        // The registered type is emitted both as typeof(...) and as the receiver of the generated member
        // access, so it must be a closed, referenceable type.
        if (!SymbolReferenceabilityHelper.IsClosedReferenceableType(declaringType, consumingAssembly)
            || ResolveDynamicDataMember(declaringType, sourceName, sourceType, consumingAssembly) is not { } memberKind)
        {
            return null;
        }

        // Resolve the optional custom display-name method (DynamicDataDisplayName /
        // DynamicDataDisplayNameDeclaringType named arguments).
        string? displayNameMethodName = null;
        string? displayNameDeclaringTypeFqn = null;
        INamedTypeSymbol displayNameDeclaringType = testMethod.ContainingType;
        foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
        {
            switch (named.Key)
            {
                case "DynamicDataDisplayName" when named.Value.Value is string name:
                    displayNameMethodName = name;
                    break;
                case "DynamicDataDisplayNameDeclaringType" when named.Value.Value is INamedTypeSymbol type:
                    displayNameDeclaringType = type;
                    break;
            }
        }

        if (displayNameMethodName is not null)
        {
            if (SymbolReferenceabilityHelper.IsClosedReferenceableType(displayNameDeclaringType, consumingAssembly)
                && IsValidDisplayNameMethod(displayNameDeclaringType, displayNameMethodName))
            {
                displayNameDeclaringTypeFqn = displayNameDeclaringType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
            }
            else
            {
                // Unresolvable / wrong-shape display-name method: leave both null so the runtime reflection
                // fallback handles it (and reports the proper diagnostic there).
                displayNameMethodName = null;
            }
        }

        return new DynamicDataSourceModel(
            DeclaringTypeFullyQualifiedName: declaringType.ToDisplayString(SymbolDisplayFormats.FullyQualified),
            SourceName: sourceName,
            MemberKind: memberKind,
            RequestedSourceType: sourceType.ToString(),
            DisplayNameDeclaringTypeFullyQualifiedName: displayNameDeclaringTypeFqn,
            DisplayNameMethodName: displayNameMethodName);
    }

    // Resolves a DynamicData source member by name to a supported, accessible, static property/method/field,
    // registering an accessor only when the generated direct member access is guaranteed to behave exactly
    // like the runtime reflection lookup in DynamicDataOperations. Anything ambiguous or not provably
    // equivalent returns null so the caller degrades to the (DAM-safe) runtime reflection fallback.
    private static DynamicDataMemberKind? ResolveDynamicDataMember(
        INamedTypeSymbol declaringType, string sourceName, DynamicDataSourceType sourceType, IAssemblySymbol consumingAssembly)
    {
        // Collect the most-derived member of each kind for the name (runtime GetProperty/GetMethod/GetField
        // use FlattenHierarchy, and C# member binding for `declaringType.Name` also selects the most-derived
        // member, so the first hit walking derived -> base is what both would pick).
        IPropertySymbol? property = null;
        IMethodSymbol? method = null;
        bool methodAmbiguous = false;
        IFieldSymbol? field = null;

        for (INamedTypeSymbol? current = declaringType;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(sourceName))
            {
                switch (member)
                {
                    case IPropertySymbol { IsIndexer: false } propertyMember:
                        property ??= propertyMember;
                        break;
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } methodMember:
                        if (method is null)
                        {
                            method = methodMember;
                        }
                        else
                        {
                            // Overloads / hidden methods make GetMethod(name) throw AmbiguousMatchException.
                            methodAmbiguous = true;
                        }

                        break;
                    case IFieldSymbol fieldMember:
                        field ??= fieldMember;
                        break;
                }
            }
        }

        // The name must map to exactly one member kind. Otherwise runtime reflection (property, then method,
        // then field) and C# member binding (most-derived, regardless of kind) can diverge, so we cannot
        // safely emit a direct access. When a kind is present, register it only if the explicit
        // DynamicDataSourceType (if any) matches the kind reflection would bind.
        return (property, method, field) switch
        {
            ({ } propertyMember, null, null) => sourceType is DynamicDataSourceType.Method or DynamicDataSourceType.Field
                ? null
                : ResolveProperty(propertyMember, consumingAssembly),
            (null, { } methodMember, null) => methodAmbiguous || sourceType is DynamicDataSourceType.Property or DynamicDataSourceType.Field
                ? null
                : ResolveMethod(methodMember, consumingAssembly),
            (null, null, { } fieldMember) => sourceType is DynamicDataSourceType.Property or DynamicDataSourceType.Method
                ? null
                : ResolveField(fieldMember, consumingAssembly),

            // Zero or multiple kinds share the name: not safe to emit a direct access.
            _ => null,
        };
    }

    private static DynamicDataMemberKind? ResolveProperty(IPropertySymbol property, IAssemblySymbol consumingAssembly)
    {
        // Runtime reads the getter via GetGetMethod(true) (non-public allowed) and requires it to be static.
        // The generated code reads it directly, so the getter itself must be static and accessible from the
        // consuming assembly (a public property with a private getter, or an internal member from another
        // assembly, would not compile). Abstract/virtual (static abstract interface) accessors can only be
        // reached through a constrained type parameter (CS8926), and a value that cannot be boxed to object?
        // (pointer / function-pointer / ref-like) cannot be returned by the delegate, so those keep the
        // reflection fallback.
        IMethodSymbol? getter = property.GetMethod;
        return getter is { IsStatic: true, IsAbstract: false, IsVirtual: false }
            && !property.IsAbstract && !property.IsVirtual
            && IsObjectConvertible(property.Type)
            && SymbolReferenceabilityHelper.IsMemberAccessibleFrom(getter.DeclaredAccessibility, getter.ContainingType, consumingAssembly)
            ? DynamicDataMemberKind.Property
            : null;
    }

    private static DynamicDataMemberKind? ResolveMethod(IMethodSymbol method, IAssemblySymbol consumingAssembly)

        // Only parameterless static non-void methods are registered. When a source method declares
        // parameters, the reflection fallback invokes it via MethodInfo.Invoke, whose default binder applies
        // primitive widening and other conversions (e.g. passing a boxed int 3 to a long parameter). A
        // generated direct call would instead unbox with an exact cast ((long)args[0]) and throw
        // InvalidCastException. Abstract/virtual (static abstract interface) methods can only be called
        // through a constrained type parameter (CS8926), and a result that cannot be boxed to object?
        // (void / by-ref / pointer / function-pointer / ref-like) cannot be returned by the delegate. Those
        // shapes keep the (DAM-safe) reflection path to preserve behavior.
        => method is { IsStatic: true, IsAbstract: false, IsVirtual: false, IsGenericMethod: false, ReturnsVoid: false, ReturnsByRef: false, Parameters.IsEmpty: true }
            && IsObjectConvertible(method.ReturnType)
            && SymbolReferenceabilityHelper.IsMemberAccessibleFrom(method.DeclaredAccessibility, method.ContainingType, consumingAssembly)
            ? DynamicDataMemberKind.Method
            : null;

    private static DynamicDataMemberKind? ResolveField(IFieldSymbol field, IAssemblySymbol consumingAssembly)
        => field.IsStatic
            && IsObjectConvertible(field.Type)
            && SymbolReferenceabilityHelper.IsMemberAccessibleFrom(field.DeclaredAccessibility, field.ContainingType, consumingAssembly)
            ? DynamicDataMemberKind.Field
            : null;

    // A value is safe to read into the generated `(object?)` delegate only when it can be boxed / implicitly
    // converted to object. Pointers, function pointers, and ref-like (ref struct) values cannot, so members of
    // those shapes must stay on the reflection fallback rather than break the generated build.
    private static bool IsObjectConvertible(ITypeSymbol type)
        => type.TypeKind is not (TypeKind.Pointer or TypeKind.FunctionPointer)
            && !type.IsRefLikeType;

    // Validates a custom display-name method exactly as DynamicDataAttribute.GetDisplayNameByReflection does:
    // GetDeclaredMethod resolves a single method declared *on the given type* (no base-type chain), which must
    // be a public static string method taking (MethodInfo, object[]) by value.
    private static bool IsValidDisplayNameMethod(INamedTypeSymbol declaringType, string methodName)
    {
        IMethodSymbol? candidate = null;
        foreach (ISymbol member in declaringType.GetMembers(methodName))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                if (candidate is not null)
                {
                    // Multiple declared overloads make GetDeclaredMethod throw AmbiguousMatchException.
                    return false;
                }

                candidate = method;
            }
        }

        // Abstract/virtual (static abstract interface) methods can only be called through a constrained type
        // parameter (CS8926), so the generated direct call would not compile; leave those to reflection.
        return candidate is { IsStatic: true, IsAbstract: false, IsVirtual: false, DeclaredAccessibility: Accessibility.Public, IsGenericMethod: false, Parameters.Length: 2 }
            && candidate.ReturnType.SpecialType == SpecialType.System_String
            && candidate.Parameters[0] is { RefKind: RefKind.None } firstParameter
            && firstParameter.Type.ToDisplayString(SymbolDisplayFormats.FullyQualified) == "global::System.Reflection.MethodInfo"
            && candidate.Parameters[1] is { RefKind: RefKind.None, Type: IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object } };
    }
}
