// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using MSTest.Analyzers.Shared;
using MSTest.AotReflection.SourceGeneration.Diagnostics;
using MSTest.AotReflection.SourceGeneration.Helpers;
using MSTest.AotReflection.SourceGeneration.Model;

namespace MSTest.AotReflection.SourceGeneration.Generators;

/// <summary>
/// Roslyn incremental generator that walks every <c>[TestClass]</c> in the consuming
/// compilation, captures the data MSTest's <c>IReflectionOperations</c> would normally
/// produce by reflection (attributes, members, parameter types, instance factories,
/// method invokers) and emits a static metadata registry the adapter can consume at
/// runtime — no reflection required.
/// </summary>
[Generator(LanguageNames.CSharp)]
internal sealed class MSTestReflectionMetadataGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // MSTestSourceGenMode selects which generator emits. This (reflection-free) generator only
        // emits when the consumer opts in via <MSTestSourceGenMode>ReflectionFree</MSTestSourceGenMode>;
        // otherwise the default rooting generator emits and this one stays fully silent so the
        // assembly is never registered twice.
        IncrementalValueProvider<bool> reflectionFree = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => SourceGenModeHelper.IsReflectionFree(provider.GlobalOptions));

        // Emit the support types (TestClassReflectionInfo, TestMethodReflectionInfo, …) once per
        // compilation, gated on reflection-free mode. They live in the consuming assembly so this has
        // no runtime dependency on a separate package.
        context.RegisterImplementationSourceOutput(reflectionFree, static (ctx, isReflectionFree) =>
        {
            if (!isReflectionFree)
            {
                return;
            }

            ctx.AddSource(
                "MSTestReflectionMetadata.SupportTypes.g.cs",
                SourceText.From(MetadataRegistryEmitter.EmitSupportTypes(), Encoding.UTF8));
        });

        IncrementalValuesProvider<TestClassTransformResult> rawResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MSTestAttributeNames.TestClass,
                // Predicate stays cheap and shape-only. Diagnostics for unsupported shapes
                // (static, generic, inaccessible, generic method, by-ref parameter) are
                // computed in BuildResult where we have the full ISymbol.
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => BuildResult(ctx, ct));

        // Surface every collected DiagnosticInfo as a real Diagnostic — but only in reflection-free
        // mode, since the unsupported-shape diagnostics describe this generator's limitations. In
        // rooting mode those shapes are handled by runtime reflection, so the warnings would be noise.
        IncrementalValuesProvider<DiagnosticInfo> diagnostics = rawResults
            .SelectMany(static (result, _) => result.Diagnostics.AsImmutableArray())
            .Combine(reflectionFree)
            .Where(static pair => pair.Right)
            .Select(static (pair, _) => pair.Left);

        context.RegisterSourceOutput(diagnostics, static (ctx, info) =>
            ctx.ReportDiagnostic(info.ToDiagnostic()));

        IncrementalValuesProvider<TestClassModel> testClasses = rawResults
            .Where(static result => result.Model is not null)
            .Select(static (result, _) => result.Model!);

        // Pull assembly-level attributes from the compilation (one value per run) and
        // wrap them in an equatable model so this branch of the pipeline can stay cached
        // when only test-class code changes.
        IncrementalValueProvider<AssemblyMetadataModel> assemblyMetadata =
            context.CompilationProvider.Select(static (c, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return new AssemblyMetadataModel(
                    TestClassModelBuilder.BuildAttributes(c.Assembly.GetAttributes(), c.Assembly));
            });

        IncrementalValueProvider<(string? AssemblyName, AssemblyMetadataModel Metadata, ImmutableArray<TestClassModel> Classes, bool ReflectionFree)> combined =
            context.CompilationProvider.Select(static (c, _) => c.AssemblyName)
                .Combine(assemblyMetadata)
                .Combine(testClasses.Collect())
                .Combine(reflectionFree)
                .Select(static (tuple, _) => (tuple.Left.Left.Left, tuple.Left.Left.Right, tuple.Left.Right, tuple.Right));

        context.RegisterImplementationSourceOutput(combined, static (ctx, payload) =>
        {
            if (!payload.ReflectionFree)
            {
                return;
            }

            string assemblyName = payload.AssemblyName ?? "Unknown";
            string source = MetadataRegistryEmitter.EmitRegistry(assemblyName, payload.Metadata, payload.Classes);
            ctx.AddSource("MSTestReflectionMetadata.Registry.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        // Emit the [ModuleInitializer] that registers this assembly with the adapter. Without it,
        // referencing this generator would emit metadata that nothing consumes. We skip emission
        // when reflection-free mode is off, or when there are no test classes — nothing to register.
        context.RegisterImplementationSourceOutput(combined, static (ctx, payload) =>
        {
            if (!payload.ReflectionFree || payload.Classes.IsDefaultOrEmpty)
            {
                return;
            }

            string source = RuntimeRegistrationEmitter.Emit(payload.Classes);
            ctx.AddSource("MSTestReflectionMetadata.Registration.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static TestClassTransformResult BuildResult(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return TestClassTransformResult.Empty;
        }

        var diagnostics = new List<DiagnosticInfo>();
        var classLocation = LocationInfo.CreateFrom(context.TargetNode);

        // Diagnostics that imply we cannot emit ANY model for this class. Reported in
        // priority order — only the first matching reason is recorded so users aren't
        // spammed with overlapping warnings (e.g. a static class is also "abstract" at the
        // IL level, but AOTSG0001 is the only one that's actionable).
        string fqn = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (typeSymbol.IsStatic)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.StaticTestClass, classLocation, fqn));
            return new TestClassTransformResult(Model: null, Diagnostics: ToEquatable(diagnostics));
        }

        if (IsGenericOrInsideGeneric(typeSymbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.GenericTestClass, classLocation, fqn));
            return new TestClassTransformResult(Model: null, Diagnostics: ToEquatable(diagnostics));
        }

        if (!SymbolAccessibilityHelper.IsAccessibleFromGeneratedCode(typeSymbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.InaccessibleTestClass, classLocation, fqn));
            return new TestClassTransformResult(Model: null, Diagnostics: ToEquatable(diagnostics));
        }

        // Abstract test classes stay silently filtered for now — they're a legitimate
        // base-class pattern and the right UX needs the concrete-derived discovery from a
        // future PR.
        if (typeSymbol.IsAbstract)
        {
            return TestClassTransformResult.Empty;
        }

        TestClassModel model = TestClassModelBuilder.Build(typeSymbol, diagnostics);
        return new TestClassTransformResult(model, ToEquatable(diagnostics));
    }

    private static EquatableArray<DiagnosticInfo> ToEquatable(List<DiagnosticInfo> diagnostics)
        => diagnostics.Count == 0
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray());

    private static bool IsGenericOrInsideGeneric(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record TestClassTransformResult(TestClassModel? Model, EquatableArray<DiagnosticInfo> Diagnostics)
    {
        public static readonly TestClassTransformResult Empty = new(null, EquatableArray<DiagnosticInfo>.Empty);
    }
}
