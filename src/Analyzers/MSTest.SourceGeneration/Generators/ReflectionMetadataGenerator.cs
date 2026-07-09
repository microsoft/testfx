// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Emitters;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Models;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.Generators;

/// <summary>
/// Incremental source generator that walks every <c>[TestClass]</c> in the compilation and emits
/// a <c>[ModuleInitializer]</c> that registers a <c>SourceGeneratedReflectionDataProvider</c> with
/// MSTest's runtime hook. When the test project opts in (by referencing this generator), MSTest
/// will read test metadata from the source-generated data instead of using runtime reflection.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ReflectionMetadataGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Display format for parameter types in <c>typeof(...)</c> expressions. Mirrors
    /// <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/> but omits <c>UseSpecialTypes</c> so
    /// primitive types emit as <c>global::System.Int32</c> rather than <c>int</c>, ensuring the
    /// generated <c>typeof()</c> calls compile in any namespace context.
    /// </summary>
    private static readonly SymbolDisplayFormat ParameterTypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TestClassMetadata?> testClasses = context.SyntaxProvider.ForAttributeWithMetadataName(
            Constants.TestClassAttributeFullName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => BuildTestClass(ctx, ct));

        IncrementalValueProvider<ImmutableArray<TestClassMetadata?>> collected = testClasses.Collect();

        // MSTestSourceGenMode selects which generator emits. This (rooting) generator emits when the
        // consumer opts into Rooting; when ReflectionFree is selected (the shipped default), the
        // reflection-free generator emits instead and this one stays silent so the assembly is never
        // registered twice.
        IncrementalValueProvider<bool> reflectionFree = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SourceGenModeHelper.IsReflectionFree(provider.GlobalOptions));

        IncrementalValueProvider<(string? AssemblyName, ImmutableArray<TestClassMetadata?> Classes, bool ReflectionFree)> source =
            context.CompilationProvider
                .Select(static (compilation, _) => compilation.AssemblyName)
                .Combine(collected)
                .Combine(reflectionFree)
                .Select(static (tuple, _) => (tuple.Left.Left, tuple.Left.Right, tuple.Right));

        context.RegisterImplementationSourceOutput(source, static (ctx, payload) =>
        {
            if (payload.ReflectionFree)
            {
                return;
            }

            EmitMetadata(ctx, (payload.AssemblyName, payload.Classes));
        });
    }

    private static TestClassMetadata? BuildTestClass(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (typeSymbol.IsAbstract || typeSymbol.IsStatic)
        {
            return null;
        }

        // Skip open generic test classes: typeof(Generic<T>) is not valid at the module-initializer
        // scope where we emit the metadata, and reflecting on an unbound generic type by method
        // signature is unreliable. Closed-constructed generics that the user writes are not
        // top-level [TestClass] declarations, so they are not seen here either.
        if (typeSymbol.IsGenericType)
        {
            return null;
        }

        // Skip types the generated module initializer (emitted as `internal`) cannot reference,
        // for example a private/protected nested [TestClass]. Emitting `typeof(Outer.PrivateNested)`
        // would fail with CS0122 inside auto-generated code.
        if (!SymbolAccessibilityHelper.IsAccessibleFromGeneratedCode(typeSymbol))
        {
            return null;
        }

        string fullyQualifiedName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string? containingNamespace = typeSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : null;

        ImmutableArray<TestMethodMetadata>.Builder methods = ImmutableArray.CreateBuilder<TestMethodMetadata>();
        var seenSignatures = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<string>.Builder baseTypes = ImmutableArray.CreateBuilder<string>();
        INamedTypeSymbol? currentType = typeSymbol;
        while (currentType is not null && currentType.SpecialType != SpecialType.System_Object)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Capture every base type that the generated module initializer can reference, so we
            // can root its members (ClassInitialize / ClassCleanup / AssemblyInitialize /
            // AssemblyCleanup / TestContext setter) via [DynamicDependency] under trimming or
            // Native AOT. Without this, those members live on the abstract base only and the
            // trimmer removes them because [DynamicDependency(All, typeof(Concrete))] does not
            // preserve base-type members. We intentionally do NOT add the base to types[] or
            // testMethods{}; runtime discovery still flows through the concrete [TestClass].
            if (!SymbolEqualityComparer.Default.Equals(currentType, typeSymbol)
                && !currentType.IsGenericType
                && SymbolAccessibilityHelper.IsAccessibleFromGeneratedCode(currentType))
            {
                baseTypes.Add(currentType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            foreach (ISymbol member in currentType.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (member is not IMethodSymbol method || method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                if (!HasTestMethodAttribute(method))
                {
                    continue;
                }

                // Skip generic test methods (e.g. `Test<T>(T value)`). Their parameter types
                // include method-level type parameters, and emitting `typeof(T)` inside the
                // non-generic module initializer does not compile. Reflection mode handles these
                // at runtime, so opting into the generator must not make a valid program fail
                // to build.
                if (method.IsGenericMethod)
                {
                    continue;
                }

                // Skip methods that take ref/out/in/ref-readonly parameters. The runtime
                // ResolveMethod compares parameter types by `typeof(T) == ParameterType`, but
                // for by-ref parameters `ParameterType` is `T&` (i.e. `MakeByRefType()`), so a
                // naive `typeof(T)` match always fails and ResolveMethod would throw inside the
                // module initializer — aborting test discovery for the whole assembly. The
                // experimental source-gen path doesn't model by-ref signatures yet, so skip.
                if (HasByRefParameter(method))
                {
                    continue;
                }

                ImmutableArray<string>.Builder parameterTypes = ImmutableArray.CreateBuilder<string>(method.Parameters.Length);
                foreach (IParameterSymbol parameter in method.Parameters)
                {
                    parameterTypes.Add(parameter.Type.ToDisplayString(ParameterTypeFormat));
                }

                ImmutableArray<string> parameterTypeArray = parameterTypes.MoveToImmutable();

                // Dedupe inherited methods by (name, parameter-signature) so an override is emitted once.
                string signature = method.Name + "(" + string.Join(",", parameterTypeArray) + ")";
                if (!seenSignatures.Add(signature))
                {
                    continue;
                }

                methods.Add(new TestMethodMetadata(method.Name, new EquatableArray<string>(parameterTypeArray)));
            }

            currentType = currentType.BaseType;
        }

        return new TestClassMetadata(
            FullyQualifiedName: fullyQualifiedName,
            DisplayName: typeSymbol.Name,
            Namespace: containingNamespace,
            Methods: new EquatableArray<TestMethodMetadata>(methods.ToImmutable()),
            BaseTypeFullyQualifiedNames: new EquatableArray<string>(baseTypes.ToImmutable()));
    }

    private static bool HasByRefParameter(IMethodSymbol method)
        => method.Parameters.Any(parameter => parameter.RefKind != RefKind.None);

    private static bool HasTestMethodAttribute(IMethodSymbol method)
    {
        foreach (INamedTypeSymbol? attributeClassCandidate in method.GetAttributes().Select(static a => a.AttributeClass))
        {
            INamedTypeSymbol? attributeClass = attributeClassCandidate;
            while (attributeClass is not null)
            {
                if (attributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    == "global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute")
                {
                    return true;
                }

                attributeClass = attributeClass.BaseType;
            }
        }

        return false;
    }

    private static void EmitMetadata(SourceProductionContext context, (string? AssemblyName, ImmutableArray<TestClassMetadata?> Classes) input)
    {
        string assemblyName = input.AssemblyName ?? "UnknownAssembly";
        ImmutableArray<TestClassMetadata> classes = input.Classes.IsDefault
            ? ImmutableArray<TestClassMetadata>.Empty
            : input.Classes.Where(static c => c is not null).Cast<TestClassMetadata>().ToImmutableArray();

        // Skip emission entirely when the compilation has no test classes — there is nothing for
        // the runtime hook to register and emitting a [ModuleInitializer] just for that adds cost.
        if (classes.IsEmpty)
        {
            return;
        }

        var metadata = new TestAssemblyMetadata(
            AssemblyName: assemblyName,
            Classes: new EquatableArray<TestClassMetadata>(classes));

        string source = ReflectionMetadataEmitter.Emit(metadata);
        context.AddSource($"{assemblyName}{Constants.GeneratedFileSuffix}", source);
    }
}
