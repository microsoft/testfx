// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
        // Emit the support types (TestClassReflectionInfo, TestMethodReflectionInfo, …) once
        // per compilation. They live in the consuming assembly so this PoC has no runtime
        // dependency on a separate package.
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource(
                "MSTestReflectionMetadata.SupportTypes.g.cs",
                SourceText.From(MetadataRegistryEmitter.EmitSupportTypes(), Encoding.UTF8)));

        IncrementalValuesProvider<TestClassTransformResult> rawResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MSTestAttributeNames.TestClass,
                // Predicate stays cheap and shape-only. Diagnostics for unsupported shapes
                // (static, generic, inaccessible, generic method, by-ref parameter) are
                // computed in BuildModel where we have the full ISymbol.
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, ct) => BuildResult(ctx, ct));

        // Surface every collected DiagnosticInfo as a real Diagnostic. Empty arrays produce
        // no work, so this branch is allocation-free for clean compilations.
        IncrementalValuesProvider<DiagnosticInfo> diagnostics = rawResults
            .SelectMany(static (result, _) => result.Diagnostics.AsImmutableArray());

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
                    TestClassModelBuilder.BuildAttributes(c.Assembly.GetAttributes()));
            });

        IncrementalValueProvider<(string? AssemblyName, AssemblyMetadataModel Metadata, ImmutableArray<TestClassModel> Classes)> combined =
            context.CompilationProvider.Select(static (c, _) => c.AssemblyName)
                .Combine(assemblyMetadata)
                .Combine(testClasses.Collect())
                .Select(static (tuple, _) => (tuple.Left.Left, tuple.Left.Right, tuple.Right));

        context.RegisterImplementationSourceOutput(combined, static (ctx, payload) =>
        {
            string assemblyName = payload.AssemblyName ?? "Unknown";
            string source = MetadataRegistryEmitter.EmitRegistry(assemblyName, payload.Metadata, payload.Classes);
            ctx.AddSource("MSTestReflectionMetadata.Registry.g.cs", SourceText.From(source, Encoding.UTF8));
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

        if (!IsReachableFromGeneratedCode(typeSymbol))
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

    private static bool IsReachableFromGeneratedCode(INamedTypeSymbol type)
    {
        // The generated registry lives in the same assembly but in a different file/type,
        // so it can reach Public / Internal / ProtectedOrInternal types (the latter being
        // "protected internal" — visible from anywhere in the same assembly). Private,
        // Protected (alone), and ProtectedAndInternal ("private protected") containing
        // types make the type unreachable.
        if (type.IsFileLocal)
        {
            return false;
        }

        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }

            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Internal:
                case Accessibility.ProtectedOrInternal:
                case Accessibility.NotApplicable:
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }

    private sealed record TestClassTransformResult(TestClassModel? Model, EquatableArray<DiagnosticInfo> Diagnostics)
    {
        public static readonly TestClassTransformResult Empty = new(null, EquatableArray<DiagnosticInfo>.Empty);
    }
}
