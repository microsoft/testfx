// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

using MSTest.AotReflection.SourceGeneration.Model;

namespace MSTest.AotReflection.SourceGeneration.Generators;

/// <summary>
/// Translates a <see cref="INamedTypeSymbol"/> decorated with <c>[TestClass]</c> into an
/// immutable, equatable <see cref="TestClassModel"/> the emitter can consume.
/// </summary>
internal static class TestClassModelBuilder
{
    private static readonly SymbolDisplayFormat FullyQualifiedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static TestClassModel Build(INamedTypeSymbol typeSymbol)
    {
        // Methods / properties are walked across the full inheritance chain (excluding
        // System.Object) so that MSTest members declared on a base class —
        // [ClassInitialize], [ClassCleanup], [TestInitialize], [TestCleanup],
        // [TestMethod], the [TestContext] setter, … — are visible to the consumer
        // without runtime reflection.
        //
        // Iteration order is derived-first so that an override or `new`-shadowed member
        // on the derived type wins over the base declaration with the same signature.
        // Constructors are NEVER inherited and are taken only from the leaf type.
        var methodsByKey = new Dictionary<string, TestMethodModel>(StringComparer.Ordinal);
        var propertiesByName = new Dictionary<string, TestPropertyModel>(StringComparer.Ordinal);
        ImmutableArray<TestMethodModel>.Builder methods = ImmutableArray.CreateBuilder<TestMethodModel>();
        ImmutableArray<TestPropertyModel>.Builder properties = ImmutableArray.CreateBuilder<TestPropertyModel>();
        ImmutableArray<TestConstructorModel>.Builder ctors = ImmutableArray.CreateBuilder<TestConstructorModel>();

        for (INamedTypeSymbol? current = typeSymbol;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            bool isLeaf = SymbolEqualityComparer.Default.Equals(current, typeSymbol);

            foreach (ISymbol member in current.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                        when IsAccessibleFromConsumer(method):
                        string key = BuildMethodSignatureKey(method);
                        if (!methodsByKey.ContainsKey(key))
                        {
                            TestMethodModel model = BuildMethod(method);
                            methodsByKey[key] = model;
                            methods.Add(model);
                        }

                        break;
                    case IPropertySymbol property
                        when IsAccessibleFromConsumer(property):
                        if (!propertiesByName.ContainsKey(property.Name))
                        {
                            TestPropertyModel model = BuildProperty(property);
                            propertiesByName[property.Name] = model;
                            properties.Add(model);
                        }

                        break;
                    case IMethodSymbol { MethodKind: MethodKind.Constructor, IsStatic: false } ctor
                        when isLeaf && ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal:
                        ctors.Add(new TestConstructorModel(BuildParameters(ctor)));
                        break;
                }
            }
        }

        return new TestClassModel(
            FullyQualifiedTypeName: typeSymbol.ToDisplayString(FullyQualifiedFormat),
            ContainingNamespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            IsAbstract: typeSymbol.IsAbstract,
            IsStatic: typeSymbol.IsStatic,
            Constructors: new EquatableArray<TestConstructorModel>(ctors.ToImmutable()),
            Methods: new EquatableArray<TestMethodModel>(methods.ToImmutable()),
            Properties: new EquatableArray<TestPropertyModel>(properties.ToImmutable()),
            Attributes: BuildAttributes(typeSymbol.GetAttributes()));
    }

    // Restricted to accessibilities the emitted helper class (a separate static type
    // declared in MSTest.SourceGenerated, not a derived type) can legally call.
    // 'protected' and 'private protected' members require the caller to be a derived
    // type, so they are excluded; 'protected internal' is included because the internal
    // half is satisfied (the generated helper lives in the same assembly).
    private static bool IsAccessibleFromConsumer(ISymbol symbol)
        => symbol.DeclaredAccessibility is
            Accessibility.Public
            or Accessibility.Internal
            or Accessibility.ProtectedOrInternal;

    private static string BuildMethodSignatureKey(IMethodSymbol method)
    {
        var sb = new StringBuilder();
        sb.Append(method.IsStatic ? "S:" : "I:");
        sb.Append(method.Name);
        sb.Append('(');
        bool first = true;
        foreach (IParameterSymbol p in method.Parameters)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            switch (p.RefKind)
            {
                case RefKind.Ref:
                    sb.Append("ref ");
                    break;
                case RefKind.Out:
                    sb.Append("out ");
                    break;
                case RefKind.In:
                    sb.Append("in ");
                    break;
            }

            sb.Append(p.Type.ToDisplayString(FullyQualifiedFormat));
        }

        sb.Append(')');
        return sb.ToString();
    }

    private static TestMethodModel BuildMethod(IMethodSymbol method)
    {
        ITypeSymbol returnType = method.ReturnType;
        string returnTypeFqn = returnType.ToDisplayString(FullyQualifiedFormat);

        bool returnsTask =
            returnTypeFqn is "global::System.Threading.Tasks.Task"
            || returnTypeFqn.StartsWith("global::System.Threading.Tasks.Task<", System.StringComparison.Ordinal);
        bool returnsValueTask =
            returnTypeFqn is "global::System.Threading.Tasks.ValueTask"
            || returnTypeFqn.StartsWith("global::System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal);
        bool returnsVoid = returnType.SpecialType == SpecialType.System_Void;

        return new TestMethodModel(
            Name: method.Name,
            IsStatic: method.IsStatic,
            IsAsync: method.IsAsync,
            ReturnsTask: returnsTask,
            ReturnsValueTask: returnsValueTask,
            ReturnsVoid: returnsVoid,
            Parameters: BuildParameters(method),
            Attributes: BuildAttributes(CollectInheritedAttributes(method)));
    }

    private static TestPropertyModel BuildProperty(IPropertySymbol property)
        => new(
            Name: property.Name,
            FullyQualifiedType: property.Type.ToDisplayString(FullyQualifiedFormat),
            HasPublicSetter: property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
            Attributes: BuildAttributes(CollectInheritedAttributes(property)));

    // Mirror the runtime behavior of MemberInfo.GetCustomAttributes(inherit: true): walk the
    // overridden-method chain and union attributes, keeping the most-derived application when
    // the same attribute type appears on multiple levels.
    private static ImmutableArray<AttributeData> CollectInheritedAttributes(IMethodSymbol method)
    {
        ImmutableArray<AttributeData> own = method.GetAttributes();
        if (method.OverriddenMethod is null)
        {
            return own;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AttributeData>.Builder builder = ImmutableArray.CreateBuilder<AttributeData>();
        AppendUnique(builder, seen, own);
        for (IMethodSymbol? baseMethod = method.OverriddenMethod; baseMethod is not null; baseMethod = baseMethod.OverriddenMethod)
        {
            AppendUnique(builder, seen, baseMethod.GetAttributes());
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<AttributeData> CollectInheritedAttributes(IPropertySymbol property)
    {
        ImmutableArray<AttributeData> own = property.GetAttributes();
        if (property.OverriddenProperty is null)
        {
            return own;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<AttributeData>.Builder builder = ImmutableArray.CreateBuilder<AttributeData>();
        AppendUnique(builder, seen, own);
        for (IPropertySymbol? baseProperty = property.OverriddenProperty; baseProperty is not null; baseProperty = baseProperty.OverriddenProperty)
        {
            AppendUnique(builder, seen, baseProperty.GetAttributes());
        }

        return builder.ToImmutable();
    }

    private static void AppendUnique(
        ImmutableArray<AttributeData>.Builder builder,
        HashSet<string> seen,
        ImmutableArray<AttributeData> attributes)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            // Attributes declared with AttributeUsage(AllowMultiple = true) may legitimately
            // appear several times across the override chain (e.g. [TestCategory]) — keep every
            // instance instead of collapsing them to one.
            if (AllowsMultiple(attributeClass))
            {
                builder.Add(attribute);
                continue;
            }

            string key = attributeClass.ToDisplayString(FullyQualifiedFormat);
            if (seen.Add(key))
            {
                builder.Add(attribute);
            }
        }
    }

    private static bool AllowsMultiple(INamedTypeSymbol attributeClass)
    {
        for (INamedTypeSymbol? current = attributeClass; current is not null; current = current.BaseType)
        {
            foreach (AttributeData attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat) != "global::System.AttributeUsageAttribute")
                {
                    continue;
                }

                foreach (KeyValuePair<string, TypedConstant> named in attribute.NamedArguments)
                {
                    if (named.Key == "AllowMultiple" && named.Value.Value is bool allowMultiple)
                    {
                        return allowMultiple;
                    }
                }

                // AttributeUsage was found on this level but did not set AllowMultiple — default is false
                // and base-level [AttributeUsage] is shadowed by the derived application per CLI rules.
                return false;
            }
        }

        return false;
    }

    private static EquatableArray<TestParameterModel> BuildParameters(IMethodSymbol method)
    {
        if (method.Parameters.IsDefaultOrEmpty)
        {
            return EquatableArray<TestParameterModel>.Empty;
        }

        TestParameterModel[] parameters = new TestParameterModel[method.Parameters.Length];
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            IParameterSymbol p = method.Parameters[i];
            parameters[i] = new TestParameterModel(p.Type.ToDisplayString(FullyQualifiedFormat), p.Name);
        }

        return new EquatableArray<TestParameterModel>(parameters.ToImmutableArray());
    }

    public static EquatableArray<AttributeApplicationModel> BuildAttributes(
        ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return EquatableArray<AttributeApplicationModel>.Empty;
        }

        return attributes
            .Select(BuildAttribute)
            .WhereNotNull()
            .ToEquatableArray();
    }

    private static AttributeApplicationModel? BuildAttribute(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { } attributeClass)
        {
            return null;
        }

        IEnumerable<TypedConstantModel> ctorArgs = attribute.ConstructorArguments.Select(ToModel);
        IEnumerable<NamedArgumentModel> namedArgs = attribute.NamedArguments.Select(static kv => new NamedArgumentModel(kv.Key, ToModel(kv.Value)));

        return new AttributeApplicationModel(
            FullyQualifiedAttributeType: attributeClass.ToDisplayString(FullyQualifiedFormat),
            ConstructorArguments: ctorArgs.ToEquatableArray(),
            NamedArguments: namedArgs.ToEquatableArray());
    }

    private static TypedConstantModel ToModel(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return new TypedConstantModel(ConstantValueKind.Null, constant.Type?.ToDisplayString(FullyQualifiedFormat), null, EquatableArray<TypedConstantModel>.Empty);
        }

        return constant.Kind switch
        {
            TypedConstantKind.Array => new TypedConstantModel(
                ConstantValueKind.Array,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                null,
                constant.Values.Select(ToModel).ToEquatableArray()),
            TypedConstantKind.Enum => new TypedConstantModel(
                ConstantValueKind.Enum,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                constant.Value,
                EquatableArray<TypedConstantModel>.Empty),
            TypedConstantKind.Type => new TypedConstantModel(
                ConstantValueKind.Type,
                (constant.Value as ITypeSymbol)?.ToDisplayString(FullyQualifiedFormat),
                null,
                EquatableArray<TypedConstantModel>.Empty),
            _ => new TypedConstantModel(
                ConstantValueKind.Primitive,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                constant.Value,
                EquatableArray<TypedConstantModel>.Empty),
        };
    }
}
