// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
/// <item><see cref="AttributeMaterializationHelper"/> decides which attributes survive trimming and converts them to models.</item>
/// <item><see cref="SymbolReferenceabilityHelper"/> provides the reusable accessibility / referenceability predicates.</item>
/// </list>
/// </remarks>
internal static class TestClassModelBuilder
{
    private const string AsyncStateMachineAttributeName = "global::System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    private const string DebuggerStepThroughAttributeName = "global::System.Diagnostics.DebuggerStepThroughAttribute";
    private const string TestClassAttributeName = "global::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute";
    private const string TestMethodAttributeName = "global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute";
    private const string DataRowAttributeName = "global::Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute";

    public static TestClassModel Build(INamedTypeSymbol typeSymbol, List<DiagnosticInfo> diagnostics)
    {
        // Methods / properties are walked across the full inheritance chain (excluding
        // System.Object) so that MSTest members declared on a base class —
        // [ClassInitialize], [ClassCleanup], [TestInitialize], [TestCleanup],
        // [TestMethod], the [TestContext] setter, … — are visible to the consumer
        // without runtime reflection.
        //
        // Iteration order is derived-first. Members declared at a nearer inheritance level
        // hide same-name ancestor members according to C# lookup rules, while overloads
        // declared together on the same type are preserved. Indexers are excluded because
        // their metadata name is not used in C# member access.
        // Constructors are NEVER inherited and are taken only from the leaf type.
        var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        var methodNamesInDerivedTypes = new HashSet<string>(StringComparer.Ordinal);
        var nonMethodNamesInDerivedTypes = new HashSet<string>(StringComparer.Ordinal);
        var methodsInDerivedTypes = new List<IMethodSymbol>();
        ImmutableArray<TestMethodModel>.Builder methods = ImmutableArray.CreateBuilder<TestMethodModel>();
        ImmutableArray<TestPropertyModel>.Builder properties = ImmutableArray.CreateBuilder<TestPropertyModel>();
        ImmutableArray<TestConstructorModel>.Builder ctors = ImmutableArray.CreateBuilder<TestConstructorModel>();
        ImmutableArray<string>.Builder baseTypes = ImmutableArray.CreateBuilder<string>();
        bool hasUnsupportedTestMethod = false;
        bool hasPartialTypeInHierarchy = false;

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
            hasPartialTypeInHierarchy |= IsPartial(current);
            ImmutableArray<ISymbol> currentMembers = current.GetMembers();

            // Capture each closed, referenceable base type so the runtime registration can root
            // its members (e.g. base-declared [ClassInitialize]/[TestContext]) via [DynamicDependency]
            // under trimming / Native AOT. Members are folded into the leaf model, but the trimmer
            // only keeps members of the concrete type unless the base is rooted explicitly too.
            if (!isLeaf && SymbolReferenceabilityHelper.IsClosedReferenceableType(current, consumingAssembly))
            {
                baseTypes.Add(current.ToDisplayString(SymbolDisplayFormats.FullyQualified));
            }

            foreach (ISymbol member in currentMembers)
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                        ImmutableArray<AttributeData> inheritedAttributes = AttributeMaterializationHelper.CollectInheritedAttributes(method);
                        bool isTestMethod = TestMemberValidationHelper.IsTestMethodAttributePresent(inheritedAttributes);
                        bool hiddenByNonMethod = nonMethodNamesInDerivedTypes.Contains(method.Name);
                        bool hiddenByMethodGroup = methodNamesInDerivedTypes.Contains(method.Name);
                        if (hiddenByNonMethod || hiddenByMethodGroup)
                        {
                            hasUnsupportedTestMethod |= isTestMethod
                                && (hiddenByNonMethod
                                    || !methodsInDerivedTypes.Any(derivedMethod =>
                                        ReplacesInheritedRuntimeTest(derivedMethod)
                                        && TestMemberValidationHelper.HaveSameRuntimeSignature(derivedMethod, method)));
                            break;
                        }

                        if (!TestMemberValidationHelper.IsAccessibleFromConsumer(method, consumingAssembly))
                        {
                            hasUnsupportedTestMethod |= isTestMethod;
                            break;
                        }

                        if (TestMemberValidationHelper.TryReportUnsupportedMethod(method, leafFqn, diagnostics))
                        {
                            hasUnsupportedTestMethod |= isTestMethod;

                            // Skip generic / by-ref methods entirely so the emitter does not produce
                            // code that references unbound type parameters or ref/in/out arguments.
                            break;
                        }

                        methods.Add(BuildMethod(method, consumingAssembly, inheritedAttributes, isTestMethod));

                        break;
                    case IPropertySymbol property:
                        hasUnsupportedTestMethod |= HasTestMethodAttribute(property.GetMethod)
                            || HasTestMethodAttribute(property.SetMethod);
                        if (methodNamesInDerivedTypes.Contains(property.Name)
                            || nonMethodNamesInDerivedTypes.Contains(property.Name))
                        {
                            break;
                        }

                        if (!property.IsIndexer
                            && seenPropertyNames.Add(property.Name)
                            && TestMemberValidationHelper.IsAccessibleFromConsumer(property, consumingAssembly))
                        {
                            properties.Add(BuildProperty(property, consumingAssembly));
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
                    case IEventSymbol eventSymbol:
                        hasUnsupportedTestMethod |= HasTestMethodAttribute(eventSymbol.AddMethod)
                            || HasTestMethodAttribute(eventSymbol.RemoveMethod)
                            || HasTestMethodAttribute(eventSymbol.RaiseMethod);
                        break;
                    case IMethodSymbol method:
                        hasUnsupportedTestMethod |= HasTestMethodAttribute(method);
                        break;
                }
            }

            foreach (ISymbol member in currentMembers)
            {
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } method:
                        methodNamesInDerivedTypes.Add(method.Name);
                        methodsInDerivedTypes.Add(method);
                        break;

                    case IPropertySymbol { IsIndexer: false }:
                    case IFieldSymbol:
                    case IEventSymbol:
                    case INamedTypeSymbol:
                        nonMethodNamesInDerivedTypes.Add(member.Name);
                        break;
                }
            }
        }

        AttributeMaterializationHelper.AttributeMaterializationResult classAttributes =
            AttributeMaterializationHelper.BuildAttributesWithCompleteness(
                AttributeMaterializationHelper.CollectInheritedAttributes(typeSymbol),
                consumingAssembly);
        bool supportsGeneratedDescriptors = classAttributes.IsComplete
            && classAttributes.Attributes.Length == 1
            && classAttributes.Attributes[0].FullyQualifiedAttributeType == TestClassAttributeName;

        var duplicateTestMethodNames = new HashSet<string>(
            methods
                .Where(static method => method.IsTestMethod)
                .GroupBy(static method => method.Name, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key),
            StringComparer.Ordinal);

        var finalizedMethods = methods
            .Select(method => duplicateTestMethodNames.Contains(method.Name)
                ? method with { IsDescriptorSupported = false }
                : method)
            .ToImmutableArray();
        bool areGeneratedDescriptorsComplete = supportsGeneratedDescriptors
            && !hasUnsupportedTestMethod
            && !hasPartialTypeInHierarchy
            && finalizedMethods.Where(static method => method.IsTestMethod).All(static method => method.IsDescriptorSupported);

        return new TestClassModel(
            FullyQualifiedTypeName: leafFqn,
            ContainingNamespace: typeSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : typeSymbol.ContainingNamespace.ToDisplayString(),
            TypeName: typeSymbol.Name,
            IsAbstract: typeSymbol.IsAbstract,
            IsStatic: typeSymbol.IsStatic,
            Constructors: new EquatableArray<TestConstructorModel>(ctors.ToImmutable()),
            Methods: new EquatableArray<TestMethodModel>(finalizedMethods),
            Properties: new EquatableArray<TestPropertyModel>(properties.ToImmutable()),
            Attributes: classAttributes.Attributes,
            AreAttributesComplete: classAttributes.IsComplete,
            SupportsGeneratedDescriptors: supportsGeneratedDescriptors,
            AreGeneratedDescriptorsComplete: areGeneratedDescriptorsComplete,
            BaseTypeFullyQualifiedNames: new EquatableArray<string>(baseTypes.ToImmutable()));
    }

    private static bool IsPartial(INamedTypeSymbol type)
        => type.DeclaringSyntaxReferences.Any(static syntaxReference =>
            syntaxReference.GetSyntax().ChildTokens().Any(static token => token.IsKind(SyntaxKind.PartialKeyword)));

    private static bool HasTestMethodAttribute(IMethodSymbol? method)
        => method is not null
        && TestMemberValidationHelper.IsTestMethodAttributePresent(AttributeMaterializationHelper.CollectInheritedAttributes(method));

    private static bool ReplacesInheritedRuntimeTest(IMethodSymbol method)
        => method.OverriddenMethod is not null
        || (method is { DeclaredAccessibility: Accessibility.Public, IsStatic: false }
            && HasTestMethodAttribute(method));

    private static TestMethodModel BuildMethod(
        IMethodSymbol method,
        IAssemblySymbol consumingAssembly,
        ImmutableArray<AttributeData> inheritedAttributes,
        bool isTestMethod)
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

        ImmutableArray<AttributeData> attributesToMaterialize = method.IsAsync
            ? inheritedAttributes.Where(static attribute => !IsCompilerSpecialAsyncAttribute(attribute)).ToImmutableArray()
            : inheritedAttributes;
        AttributeMaterializationHelper.AttributeMaterializationResult methodAttributes =
            AttributeMaterializationHelper.BuildAttributesWithCompleteness(attributesToMaterialize, consumingAssembly);
        bool isDescriptorSupported = isTestMethod
            && methodAttributes.IsComplete
            && method.DeclaredAccessibility == Accessibility.Public
            && !method.IsStatic
            && !method.IsAbstract
            && !method.IsAsync
            && returnsVoid
            && HasOnlyDescriptorSupportedAttributes(methodAttributes.Attributes);

        return new TestMethodModel(
            Name: method.Name,
            IsStatic: method.IsStatic,
            IsAsync: method.IsAsync,
            ReturnsTask: returnsTask,
            ReturnsValueTask: returnsValueTask,
            ReturnsVoid: returnsVoid,
            IsTestMethod: isTestMethod,
            IsDescriptorSupported: isDescriptorSupported,
            Parameters: BuildParameters(method),
            Attributes: methodAttributes.Attributes,
            AreAttributesComplete: methodAttributes.IsComplete,
            DynamicDataSources: DynamicDataSourceBuilder.BuildDynamicDataSources(inheritedAttributes, method, consumingAssembly));
    }

    private static bool HasOnlyDescriptorSupportedAttributes(EquatableArray<AttributeApplicationModel> attributes)
    {
        int testMethodAttributeCount = 0;
        foreach (AttributeApplicationModel attribute in attributes)
        {
            switch (attribute.FullyQualifiedAttributeType)
            {
                case TestMethodAttributeName:
                    testMethodAttributeCount++;
                    break;

                case DataRowAttributeName:
                    break;

                default:
                    return false;
            }
        }

        return testMethodAttributeCount == 1;
    }

    private static bool IsCompilerSpecialAsyncAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormats.FullyQualified) is
            AsyncStateMachineAttributeName or DebuggerStepThroughAttributeName;

    private static TestPropertyModel BuildProperty(IPropertySymbol property, IAssemblySymbol consumingAssembly)
        => new(
            Name: property.Name,
            FullyQualifiedType: property.Type.ToDisplayString(SymbolDisplayFormats.FullyQualified),
            IsStatic: property.IsStatic,

            HasGettableValue: property.GetMethod is { } getter
                && SymbolReferenceabilityHelper.IsMemberAccessibleFrom(
                    getter.DeclaredAccessibility,
                    getter.ContainingType,
                    consumingAssembly),
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
            var namedType = p.Type as INamedTypeSymbol;
            if (namedType?.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                namedType = namedType.TypeArguments[0] as INamedTypeSymbol;
            }

            string? enumFullyQualifiedType = namedType?.TypeKind == TypeKind.Enum
                ? namedType.ToDisplayString(SymbolDisplayFormats.FullyQualified)
                : null;
            parameters[i] = new TestParameterModel(
                p.Type.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                p.Name,
                enumFullyQualifiedType);
        }

        return new EquatableArray<TestParameterModel>(parameters.ToImmutableArray());
    }
}
