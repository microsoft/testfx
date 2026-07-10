// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

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

        // Generated registration lives in the leaf type's (the compilation's) assembly, so attribute
        // materializability is judged from there — even for members inherited from a base type in
        // another assembly.
        IAssemblySymbol consumingAssembly = typeSymbol.ContainingAssembly;

        for (INamedTypeSymbol? current = typeSymbol;
             current is not null && current.SpecialType != SpecialType.System_Object;
             current = current.BaseType)
        {
            bool isLeaf = SymbolEqualityComparer.Default.Equals(current, typeSymbol);

            // Capture each accessible, non-generic base type so the runtime registration can root
            // its members (e.g. base-declared [ClassInitialize]/[TestContext]) via [DynamicDependency]
            // under trimming / Native AOT. Members are folded into the leaf model, but the trimmer
            // only keeps members of the concrete type unless the base is rooted explicitly too.
            if (!isLeaf && !current.IsGenericType && SymbolAccessibilityHelper.IsAccessibleFromGeneratedCode(current))
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
                            TestMethodModel model = BuildMethod(method, typeSymbol, consumingAssembly);
                            methodsByKey[key] = model;
                            methods.Add(model);
                        }

                        break;
                    case IPropertySymbol property
                        when !property.IsIndexer && IsAccessibleFromConsumer(property):
                        if (!propertiesByName.ContainsKey(property.Name))
                        {
                            TestPropertyModel model = BuildProperty(property, consumingAssembly);
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

                        // MSTest only ever instantiates a test class through a parameterless ctor or a
                        // single-TestContext ctor (the adapter's TypeCache prefers the TestContext ctor and
                        // otherwise takes the parameterless one). Registering any other shape would be dead
                        // — and, because the runtime matches invokers by argument type, an extra compatible
                        // overload (e.g. ctor(object)) could be picked over the intended one. Only emit the
                        // two supported shapes so the type-level lookup stays unambiguous.
                        if (!IsSupportedTestClassConstructor(ctor))
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
            Attributes: BuildAttributes(typeSymbol.GetAttributes(), consumingAssembly),
            BaseTypeFullyQualifiedNames: new EquatableArray<string>(baseTypes.ToImmutable()));
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

    private static TestMethodModel BuildMethod(IMethodSymbol method, INamedTypeSymbol testClassSymbol, IAssemblySymbol consumingAssembly)
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
            Attributes: BuildAttributes(inheritedAttributes, consumingAssembly),
            DataRows: BuildDataRows(inheritedAttributes),
            DynamicDataSources: BuildDynamicDataSources(inheritedAttributes, testClassSymbol));
    }

    // Resolves each [DynamicData(...)] on a test method to a concrete source member (and optional custom
    // display-name method) at compile time so the generator can register a reflection-free accessor with
    // DynamicDataSourceResolver. Sources the generator cannot resolve (missing member, inaccessible, wrong
    // shape) are skipped: at runtime DynamicDataOperations falls back to reflection for those.
    private static EquatableArray<DynamicDataSourceModel> BuildDynamicDataSources(ImmutableArray<AttributeData> attributes, INamedTypeSymbol testClassSymbol)
    {
        if (attributes.IsDefaultOrEmpty)
        {
            return EquatableArray<DynamicDataSourceModel>.Empty;
        }

        ImmutableArray<DynamicDataSourceModel>.Builder? builder = null;
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass?.ToDisplayString(FullyQualifiedFormat) != "global::" + MSTestAttributeNames.DynamicData)
            {
                continue;
            }

            if (TryBuildDynamicDataSource(attribute, testClassSymbol) is { } model)
            {
                (builder ??= ImmutableArray.CreateBuilder<DynamicDataSourceModel>()).Add(model);
            }
        }

        return builder is null
            ? EquatableArray<DynamicDataSourceModel>.Empty
            : new EquatableArray<DynamicDataSourceModel>(builder.ToImmutable());
    }

    private static DynamicDataSourceModel? TryBuildDynamicDataSource(AttributeData attribute, INamedTypeSymbol testClassSymbol)
    {
        ImmutableArray<TypedConstant> ctorArgs = attribute.ConstructorArguments;
        if (ctorArgs.IsDefaultOrEmpty || ctorArgs[0].Value is not string sourceName)
        {
            return null;
        }

        // The declaring type is either an explicit typeof(...) constructor argument or, by default, the test
        // class. (DynamicDataSourceType and the params object[] arguments do not affect member resolution here.)
        INamedTypeSymbol declaringType = testClassSymbol;
        for (int i = 1; i < ctorArgs.Length; i++)
        {
            if (ctorArgs[i].Kind == TypedConstantKind.Type && ctorArgs[i].Value is INamedTypeSymbol explicitType)
            {
                declaringType = explicitType;
                break;
            }
        }

        if (ResolveDynamicDataMember(declaringType, sourceName) is not { } resolved)
        {
            return null;
        }

        (DynamicDataMemberKind memberKind, EquatableArray<string> methodParameterTypes) = resolved;

        // Resolve the optional custom display-name method (DynamicDataDisplayName /
        // DynamicDataDisplayNameDeclaringType named arguments).
        string? displayNameMethodName = null;
        string? displayNameDeclaringTypeFqn = null;
        INamedTypeSymbol displayNameDeclaringType = testClassSymbol;
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
            if (IsValidDisplayNameMethod(displayNameDeclaringType, displayNameMethodName))
            {
                displayNameDeclaringTypeFqn = displayNameDeclaringType.ToDisplayString(FullyQualifiedFormat);
            }
            else
            {
                // Unresolvable / wrong-shape display-name method: leave both null so the runtime reflection
                // fallback handles it (and reports the proper diagnostic there).
                displayNameMethodName = null;
            }
        }

        return new DynamicDataSourceModel(
            DeclaringTypeFullyQualifiedName: declaringType.ToDisplayString(FullyQualifiedFormat),
            SourceName: sourceName,
            MemberKind: memberKind,
            MethodParameterTypes: methodParameterTypes,
            DisplayNameDeclaringTypeFullyQualifiedName: displayNameDeclaringTypeFqn,
            DisplayNameMethodName: displayNameMethodName);
    }

    // Resolves a DynamicData source member by name to a supported, accessible, static property/method/field.
    // Mirrors DynamicDataOperations' AutoDetect order (property, then method, then field). Returns null when
    // nothing suitable is found so the caller degrades to the runtime reflection fallback.
    private static (DynamicDataMemberKind Kind, EquatableArray<string> MethodParameterTypes)? ResolveDynamicDataMember(INamedTypeSymbol declaringType, string sourceName)
    {
        for (INamedTypeSymbol? current = declaringType; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(sourceName))
            {
                switch (member)
                {
                    case IPropertySymbol { IsStatic: true, GetMethod: not null } property when IsAccessibleFromConsumer(property):
                        return (DynamicDataMemberKind.Property, EquatableArray<string>.Empty);

                    case IMethodSymbol { IsStatic: true, MethodKind: MethodKind.Ordinary, IsGenericMethod: false } dataMethod when IsAccessibleFromConsumer(dataMethod):
                        ImmutableArray<string>.Builder parameterTypes = ImmutableArray.CreateBuilder<string>(dataMethod.Parameters.Length);
                        foreach (IParameterSymbol parameter in dataMethod.Parameters)
                        {
                            if (parameter.RefKind != RefKind.None)
                            {
                                return null;
                            }

                            parameterTypes.Add(parameter.Type.ToDisplayString(FullyQualifiedFormat));
                        }

                        return (DynamicDataMemberKind.Method, new EquatableArray<string>(parameterTypes.ToImmutable()));

                    case IFieldSymbol { IsStatic: true } field when IsAccessibleFromConsumer(field):
                        return (DynamicDataMemberKind.Field, EquatableArray<string>.Empty);
                }
            }
        }

        return null;
    }

    private static bool IsValidDisplayNameMethod(INamedTypeSymbol declaringType, string methodName)
    {
        for (INamedTypeSymbol? current = declaringType; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (ISymbol member in current.GetMembers(methodName))
            {
                if (member is IMethodSymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 2, IsGenericMethod: false } method
                    && method.ReturnType.SpecialType == SpecialType.System_String
                    && method.Parameters[0].Type.ToDisplayString(FullyQualifiedFormat) == "global::System.Reflection.MethodInfo"
                    && method.Parameters[1].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Object })
                {
                    return true;
                }
            }
        }

        return false;
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

    private static TestPropertyModel BuildProperty(IPropertySymbol property, IAssemblySymbol consumingAssembly)
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
            // An init-only setter has public DeclaredAccessibility but cannot be assigned outside an
            // object initializer, so emitting `instance.Prop = value` would not compile (CS8852);
            // treat it as non-settable so the adapter falls back to reflection (PropertyInfo.SetValue).
            HasPublicSetter: property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false },
            Attributes: BuildAttributes(CollectInheritedAttributes(property), consumingAssembly));

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

    private static bool IsSupportedTestClassConstructor(IMethodSymbol constructor)
    {
        ImmutableArray<IParameterSymbol> parameters = constructor.Parameters;
        return parameters.Length == 0
            || (parameters.Length == 1
                && parameters[0].Type.ToDisplayString(FullyQualifiedFormat) == "global::" + MSTestAttributeNames.UnitTestingNamespace + ".TestContext");
    }

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
        ImmutableArray<AttributeData> attributes,
        IAssemblySymbol consumingAssembly)
        => attributes.IsDefaultOrEmpty
            ? EquatableArray<AttributeApplicationModel>.Empty
            : attributes
                .Select(attribute => BuildAttribute(attribute, consumingAssembly))
                .WhereNotNull()
                .ToEquatableArray();

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
            FullyQualifiedAttributeType: attributeClass.ToDisplayString(FullyQualifiedFormat),
            ConstructorArguments: ctorArgs.ToEquatableArray(),
            NamedArguments: namedArgs.ToEquatableArray());
    }

    private static bool IsAttributeMaterializable(AttributeData attribute, INamedTypeSymbol attributeClass, IAssemblySymbol consumingAssembly)
        // The attribute type (and every enclosing type) must be referenceable; the constructor the
        // generated `new T(...)` binds to must be callable (a null AttributeConstructor — Roslyn could
        // not resolve it — is treated as not materializable); and every argument type the emitter
        // writes out (enum casts, typeof targets, typed nulls, nested array elements) must also be
        // referenceable.
        => IsTypeReferenceableFrom(attributeClass, consumingAssembly)
            && attribute.AttributeConstructor is { } constructor
            && IsMemberAccessibleFrom(constructor.DeclaredAccessibility, constructor.ContainingType, consumingAssembly)
            && attribute.ConstructorArguments.All(argument => AreArgumentTypesReferenceable(argument, consumingAssembly))
            && attribute.NamedArguments.All(named => AreArgumentTypesReferenceable(named.Value, consumingAssembly));

    private static bool AreArgumentTypesReferenceable(TypedConstant constant, IAssemblySymbol consumingAssembly)
        => constant.Kind switch
        {
            TypedConstantKind.Array => constant.Values.All(element => AreArgumentTypesReferenceable(element, consumingAssembly)),

            // typeof(X): the target type must be referenceable. Non-named targets (arrays, type
            // parameters) are conservatively rejected.
            TypedConstantKind.Type => constant.Value is null
                || (constant.Value is INamedTypeSymbol typeofTarget && IsTypeReferenceableFrom(typeofTarget, consumingAssembly)),

            // Enum casts and typed nulls emit a `(Type)` cast, so the constant's declared type must be
            // referenceable. Untyped values (Type is null) are plain literals.
            _ => constant.Type is not INamedTypeSymbol namedType
                || IsTypeReferenceableFrom(namedType, consumingAssembly),
        };

    private static bool IsTypeReferenceableFrom(INamedTypeSymbol type, IAssemblySymbol consumingAssembly)
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

    private static bool IsMemberAccessibleFrom(Accessibility accessibility, INamedTypeSymbol containingType, IAssemblySymbol consumingAssembly)
        => IsMemberAccessibleFrom(accessibility, containingType.ContainingAssembly, consumingAssembly);

    private static bool IsMemberAccessibleFrom(Accessibility accessibility, IAssemblySymbol? declaringAssembly, IAssemblySymbol consumingAssembly)
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
