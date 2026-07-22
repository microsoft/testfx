// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Diagnostics;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Translates a <see cref="INamedTypeSymbol"/> decorated with <c>[TestClass]</c> into an
/// immutable, equatable <see cref="TestClassModel"/> the emitter can consume.
/// </summary>
/// <remarks>
/// This type owns the top-level orchestration — walking the inheritance chain and assembling the model —
/// and delegates the specialized subsystems to focused helpers:
/// <list type="bullet">
/// <item><see cref="DynamicDataSourceBuilder"/> resolves <c>[DynamicData]</c> sources.</item>
/// <item><see cref="DataRowBuilder"/> parses <c>[DataRow]</c> applications.</item>
/// <item><see cref="AttributeMaterializationHelper"/> decides which attributes survive trimming and converts them to models.</item>
/// <item><see cref="SymbolReferenceabilityHelper"/> provides the reusable accessibility / referenceability predicates.</item>
/// </list>
/// </remarks>
internal static class TestClassModelBuilder
{
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

        string leafFqn = typeSymbol.ToDisplayString(SymbolDisplayFormats.FullyQualified);

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
                baseTypes.Add(current.ToDisplayString(SymbolDisplayFormats.FullyQualified));
            }

            foreach (ISymbol member in current.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                        when TestMemberValidationHelper.IsAccessibleFromConsumer(method):
                        if (TestMemberValidationHelper.TryReportUnsupportedMethod(method, leafFqn, diagnostics))
                        {
                            // Skip generic / by-ref methods entirely so the emitter does not produce
                            // code that references unbound type parameters or ref/in/out arguments.
                            break;
                        }

                        string key = TestMemberValidationHelper.BuildMethodSignatureKey(method);
                        if (!methodsByKey.ContainsKey(key))
                        {
                            TestMethodModel model = BuildMethod(method, consumingAssembly);
                            methodsByKey[key] = model;
                            methods.Add(model);
                        }

                        break;
                    case IPropertySymbol property
                        when !property.IsIndexer && TestMemberValidationHelper.IsAccessibleFromConsumer(property):
                        if (!propertiesByName.ContainsKey(property.Name))
                        {
                            TestPropertyModel model = BuildProperty(property, consumingAssembly);
                            propertiesByName[property.Name] = model;
                            properties.Add(model);
                        }

                        break;
                    case IMethodSymbol { MethodKind: MethodKind.Constructor, IsStatic: false } ctor
                        when isLeaf && ctor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal:
                        if (TestMemberValidationHelper.TryReportUnsupportedMethod(ctor, leafFqn, diagnostics))
                        {
                            break;
                        }

                        // MSTest only ever instantiates a test class through a parameterless ctor or a
                        // single-TestContext ctor (the adapter's TypeCache prefers the TestContext ctor and
                        // otherwise takes the parameterless one). Registering any other shape would be dead
                        // — and, because the runtime matches invokers by argument type, an extra compatible
                        // overload (e.g. ctor(object)) could be picked over the intended one. Only emit the
                        // two supported shapes so the type-level lookup stays unambiguous.
                        if (!TestMemberValidationHelper.IsSupportedTestClassConstructor(ctor))
                        {
                            break;
                        }

                        ctors.Add(new TestConstructorModel(BuildParameters(ctor)));
                        break;
                }
            }
        }

        AttributeMaterializationHelper.AttributeMaterializationResult classAttributes =
            AttributeMaterializationHelper.BuildAttributesWithCompleteness(
                AttributeMaterializationHelper.CollectInheritedAttributes(typeSymbol),
                consumingAssembly);

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
            Attributes: classAttributes.Attributes,
            AreAttributesComplete: classAttributes.IsComplete,
            BaseTypeFullyQualifiedNames: new EquatableArray<string>(baseTypes.ToImmutable()));
    }

    private static TestMethodModel BuildMethod(IMethodSymbol method, IAssemblySymbol consumingAssembly)
    {
        ITypeSymbol returnType = method.ReturnType;
        string returnTypeFqn = returnType.ToDisplayString(SymbolDisplayFormats.FullyQualified);

        bool returnsTask =
            returnTypeFqn is "global::System.Threading.Tasks.Task"
            || returnTypeFqn.StartsWith("global::System.Threading.Tasks.Task<", System.StringComparison.Ordinal);
        bool returnsValueTask =
            returnTypeFqn is "global::System.Threading.Tasks.ValueTask"
            || returnTypeFqn.StartsWith("global::System.Threading.Tasks.ValueTask<", System.StringComparison.Ordinal);
        bool returnsVoid = returnType.SpecialType == SpecialType.System_Void;

        ImmutableArray<AttributeData> inheritedAttributes = AttributeMaterializationHelper.CollectInheritedAttributes(method);
        AttributeMaterializationHelper.AttributeMaterializationResult methodAttributes =
            AttributeMaterializationHelper.BuildAttributesWithCompleteness(inheritedAttributes, consumingAssembly);

        return new TestMethodModel(
            Name: method.Name,
            IsStatic: method.IsStatic,
            IsAsync: method.IsAsync,
            ReturnsTask: returnsTask,
            ReturnsValueTask: returnsValueTask,
            ReturnsVoid: returnsVoid,
            IsTestMethod: TestMemberValidationHelper.IsTestMethodAttributePresent(method),
            Parameters: BuildParameters(method),
            Attributes: methodAttributes.Attributes,
            AreAttributesComplete: methodAttributes.IsComplete,
            DataRows: DataRowBuilder.BuildDataRows(inheritedAttributes),
            DynamicDataSources: DynamicDataSourceBuilder.BuildDynamicDataSources(inheritedAttributes, method, consumingAssembly));
    }

    private static TestPropertyModel BuildProperty(IPropertySymbol property, IAssemblySymbol consumingAssembly)
        => new(
            Name: property.Name,
            FullyQualifiedType: property.Type.ToDisplayString(SymbolDisplayFormats.FullyQualified),
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
            Attributes: AttributeMaterializationHelper.BuildAttributes(AttributeMaterializationHelper.CollectInheritedAttributes(property), consumingAssembly));

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
            parameters[i] = new TestParameterModel(p.Type.ToDisplayString(SymbolDisplayFormats.FullyQualified), p.Name);
        }

        return new EquatableArray<TestParameterModel>(parameters.ToImmutableArray());
    }
}
