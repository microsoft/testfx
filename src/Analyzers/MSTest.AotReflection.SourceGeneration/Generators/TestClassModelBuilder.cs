// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

using MSTest.AotReflection.SourceGeneration.Diagnostics;
using MSTest.AotReflection.SourceGeneration.Helpers;
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

    public static TestClassModel Build(INamedTypeSymbol typeSymbol, List<DiagnosticInfo> diagnostics)
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
        ImmutableArray<string>.Builder baseTypes = ImmutableArray.CreateBuilder<string>();

        string leafFqn = typeSymbol.ToDisplayString(FullyQualifiedFormat);

        for (INamedTypeSymbol? current = typeSymbol;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            bool isLeaf = SymbolEqualityComparer.Default.Equals(current, typeSymbol);

            // Capture each accessible, non-generic base type so the runtime registration can root
            // its members (e.g. base-declared [ClassInitialize]/[TestContext]) via [DynamicDependency]
            // under trimming / Native AOT. Members are folded into the leaf model, but the trimmer
            // only keeps members of the concrete type unless the base is rooted explicitly too.
            if (!isLeaf && !current.IsGenericType && IsTypeReachableFromGeneratedCode(current))
            {
                baseTypes.Add(current.ToDisplayString(FullyQualifiedFormat));
            }

            foreach (ISymbol member in current.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                        when IsAccessibleFromConsumer(method):
                        if (TryReportUnsupportedMethod(method, leafFqn, diagnostics))
                        {
                            // Skip generic / by-ref methods entirely so the emitter does not produce
                            // code that references unbound type parameters or ref/in/out arguments.
                            break;
                        }

                        string key = BuildMethodSignatureKey(method);
                        if (!methodsByKey.ContainsKey(key))
                        {
                            TestMethodModel model = BuildMethod(method);
                            methodsByKey[key] = model;
                            methods.Add(model);
                        }

                        break;
                    case IPropertySymbol property
                        when !property.IsIndexer && IsAccessibleFromConsumer(property):
                        if (!propertiesByName.ContainsKey(property.Name))
                        {
                            TestPropertyModel model = BuildProperty(property);
                            propertiesByName[property.Name] = model;
                            properties.Add(model);
                        }

                        break;
                    case IMethodSymbol { MethodKind: MethodKind.Constructor, IsStatic: false } ctor
                        when isLeaf && ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal:
                        if (TryReportUnsupportedMethod(ctor, leafFqn, diagnostics))
                        {
                            break;
                        }

                        ctors.Add(new TestConstructorModel(BuildParameters(ctor)));
                        break;
                }
            }
        }

        return new TestClassModel(
            FullyQualifiedTypeName: leafFqn,
            ContainingNamespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            IsAbstract: typeSymbol.IsAbstract,
            IsStatic: typeSymbol.IsStatic,
            Constructors: new EquatableArray<TestConstructorModel>(ctors.ToImmutable()),
            Methods: new EquatableArray<TestMethodModel>(methods.ToImmutable()),
            Properties: new EquatableArray<TestPropertyModel>(properties.ToImmutable()),
            Attributes: BuildAttributes(typeSymbol.GetAttributes()),
            BaseTypeFullyQualifiedNames: new EquatableArray<string>(baseTypes.ToImmutable()));
    }

    // The generated registration lives in the same assembly as the test class, so a type is
    // reachable when it (and every enclosing type) is at least internal and not file-local.
    private static bool IsTypeReachableFromGeneratedCode(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Private:
                case Accessibility.Protected:
                case Accessibility.ProtectedAndInternal:
                    return false;
            }
        }

        return true;
    }

    private static bool IsTestMethodAttributePresent(IMethodSymbol method)
    {
        foreach (AttributeData attribute in method.GetAttributes())
        {
            for (INamedTypeSymbol? attributeClass = attribute.AttributeClass;
                 attributeClass is not null;
                 attributeClass = attributeClass.BaseType)
            {
                if (attributeClass.ToDisplayString(FullyQualifiedFormat) == "global::" + MSTestAttributeNames.TestMethod)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Reports AOTSG0004 (generic method) and AOTSG0005 (by-ref parameter) when applicable.
    // Returns true if the member must be excluded from the emitted model.
    private static bool TryReportUnsupportedMethod(IMethodSymbol method, string owningClassFqn, List<DiagnosticInfo> diagnostics)
    {
        bool unsupported = false;

        // AOTSG0004 only applies to ordinary methods. Constructors cannot be generic so
        // method.IsGenericMethod is false for them.
        if (method.IsGenericMethod)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.GenericTestMethod,
                LocationInfo.CreateFrom(method),
                owningClassFqn,
                method.Name));
            unsupported = true;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ByRefParameter,
                    LocationInfo.CreateFrom(parameter),
                    owningClassFqn,
                    method.MethodKind == MethodKind.Constructor ? "ctor" : method.Name,
                    parameter.Name));
                unsupported = true;
            }
        }

        return unsupported;
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
        if (method.Arity > 0)
        {
            sb.Append('`');
            sb.Append(method.Arity);
        }

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

        ImmutableArray<AttributeData> inheritedAttributes = CollectInheritedAttributes(method);

        return new TestMethodModel(
            Name: method.Name,
            IsStatic: method.IsStatic,
            IsAsync: method.IsAsync,
            ReturnsTask: returnsTask,
            ReturnsValueTask: returnsValueTask,
            ReturnsVoid: returnsVoid,
            IsTestMethod: IsTestMethodAttributePresent(method),
            Parameters: BuildParameters(method),
            Attributes: BuildAttributes(inheritedAttributes),
            DataRows: BuildDataRows(inheritedAttributes));
    }

    // Walks the attribute list and reifies each [DataRow(...)] application into a flat
    // object?[] row. Mirrors DataRowAttribute's runtime behavior: when the constructor uses
    // the variadic overload (object? data1, params object?[] moreData), Roslyn surfaces the
    // tail as a single Array TypedConstant, which we flatten back so the consumer sees the
    // same shape as DataRowAttribute.Data.
    private static EquatableArray<DataRowModel> BuildDataRows(ImmutableArray<AttributeData> attributes)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return EquatableArray<DataRowModel>.Empty;
        }

        ImmutableArray<DataRowModel>.Builder builder = ImmutableArray.CreateBuilder<DataRowModel>();
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is not { } attributeClass)
            {
                continue;
            }

            if (attributeClass.ToDisplayString(FullyQualifiedFormat) != "global::" + MSTestAttributeNames.DataRow)
            {
                continue;
            }

            ImmutableArray<TypedConstant> ctorArgs = attribute.ConstructorArguments;
            ImmutableArray<TypedConstantModel>.Builder rowBuilder = ImmutableArray.CreateBuilder<TypedConstantModel>();

            bool lastIsParamsArray =
                attribute.AttributeConstructor is { Parameters: { IsDefaultOrEmpty: false } parameters }
                && parameters[parameters.Length - 1].IsParams
                && !ctorArgs.IsDefaultOrEmpty
                && ctorArgs[ctorArgs.Length - 1].Kind == TypedConstantKind.Array;

            for (int i = 0; i < ctorArgs.Length; i++)
            {
                if (i == ctorArgs.Length - 1 && lastIsParamsArray)
                {
                    foreach (TypedConstant element in ctorArgs[i].Values)
                    {
                        rowBuilder.Add(ToModel(element));
                    }
                }
                else
                {
                    rowBuilder.Add(ToModel(ctorArgs[i]));
                }
            }

            builder.Add(new DataRowModel(new EquatableArray<TypedConstantModel>(rowBuilder.ToImmutable())));
        }

        return new EquatableArray<DataRowModel>(builder.ToImmutable());
    }

    private static TestPropertyModel BuildProperty(IPropertySymbol property)
        => new(
            Name: property.Name,
            FullyQualifiedType: property.Type.ToDisplayString(FullyQualifiedFormat),
            IsStatic: property.IsStatic,

            // The generated registry lives in the consuming assembly, so a getter is reachable
            // when it is public, internal, or protected-internal. private / protected getters
            // cannot be read from the generated (non-derived) call site.
            HasGettableValue: property.GetMethod is
            {
                DeclaredAccessibility: Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal,
            },
            HasPublicSetter: property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
            Attributes: BuildAttributes(CollectInheritedAttributes(property)));

    // Mirror the runtime behavior of MemberInfo.GetCustomAttributes(inherit: true): walk the
    // overridden-member chain, honor AttributeUsageAttribute.Inherited, and keep only the
    // most-derived application for attributes that do not allow multiple instances.
    private static ImmutableArray<AttributeData> CollectInheritedAttributes(IMethodSymbol method)
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

    private static ImmutableArray<AttributeData> CollectInheritedAttributes(IPropertySymbol property)
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

            string key = attributeClass.ToDisplayString(FullyQualifiedFormat);
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
            if (attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat) != "global::System.AttributeUsageAttribute")
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

    private static EquatableArray<TestParameterModel> BuildParameters(IMethodSymbol method)
    {
        if (method.Parameters.IsDefaultOrEmpty)
        {
            return EquatableArray<TestParameterModel>.Empty;
        }

        var parameters = new TestParameterModel[method.Parameters.Length];
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            IParameterSymbol p = method.Parameters[i];
            parameters[i] = new TestParameterModel(p.Type.ToDisplayString(FullyQualifiedFormat), p.Name);
        }

        return new EquatableArray<TestParameterModel>(parameters.ToImmutableArray());
    }

    public static EquatableArray<AttributeApplicationModel> BuildAttributes(
        ImmutableArray<AttributeData> attributes)
        => attributes.IsDefaultOrEmpty
            ? EquatableArray<AttributeApplicationModel>.Empty
            : attributes
                .Select(BuildAttribute)
                .WhereNotNull()
                .ToEquatableArray();

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
        => constant switch
        {
            { IsNull: true } => new TypedConstantModel(
                ConstantValueKind.Null,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                null,
                EquatableArray<TypedConstantModel>.Empty),
            { Kind: TypedConstantKind.Array } => new TypedConstantModel(
                ConstantValueKind.Array,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                null,
                constant.Values.Select(ToModel).ToEquatableArray()),
            { Kind: TypedConstantKind.Enum } => new TypedConstantModel(
                ConstantValueKind.Enum,
                constant.Type?.ToDisplayString(FullyQualifiedFormat),
                constant.Value,
                EquatableArray<TypedConstantModel>.Empty),
            { Kind: TypedConstantKind.Type } => new TypedConstantModel(
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
