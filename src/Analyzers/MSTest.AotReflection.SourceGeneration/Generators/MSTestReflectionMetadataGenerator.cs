// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

        IncrementalValuesProvider<TestClassModel> testClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MSTestAttributeNames.TestClass,
                predicate: static (node, _) =>
                    node is TypeDeclarationSyntax type
                    && !type.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)),
                transform: static (ctx, ct) => BuildModel(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

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

    private static TestClassModel? BuildModel(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        // Skip abstract / static / generic classes for this PoC — they need extra wiring.
        if (typeSymbol.IsAbstract || typeSymbol.IsStatic || typeSymbol.IsGenericType)
        {
            return null;
        }

        return TestClassModelBuilder.Build(typeSymbol);
    }
}
