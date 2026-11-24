// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class MSTestHostInjectionMetadataGeneratorTests
{
    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    public async Task Generate_StaticInvocation_EmitsMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language);

        AssertGeneratedMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    public async Task Generate_ReducedExtensionInvocation_EmitsMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, reducedExtension: true);

        AssertGeneratedMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    public async Task Generate_RepeatedInvocations_EmitsMetadataOnce(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, invocationCount: 3);

        AssertGeneratedMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    public async Task Generate_InvocationRemoved_RemovesMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, removeInvocationAfterFirstRun: true);

        AssertNoMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp, "assembly")]
    [DataRow(LanguageNames.CSharp, "namespace")]
    [DataRow(LanguageNames.CSharp, "type")]
    [DataRow(LanguageNames.CSharp, "method")]
    [DataRow(LanguageNames.CSharp, "zero-parameters")]
    [DataRow(LanguageNames.CSharp, "two-parameters")]
    [DataRow(LanguageNames.CSharp, "parameter-type")]
    [DataRow(LanguageNames.CSharp, "parameter-interface")]
    [DataRow(LanguageNames.CSharp, "return-type")]
    public async Task Generate_LookalikeApi_DoesNotEmitMetadata(string language, string scenario)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, scenario);

        AssertNoMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    public async Task Generate_NoCandidateInvocation_DoesNotEmitMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, invocationCount: 0);

        AssertNoMetadata(result, output);
    }

    [TestMethod]
    public async Task Generate_NameofMention_DoesNotEmitMetadata()
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(LanguageNames.CSharp, nameofOnly: true);

        AssertNoMetadata(result, output);
    }

    private static async Task<(GeneratorDriverRunResult Result, Compilation Output)> RunGeneratorAsync(
        string language,
        string scenario = "valid",
        bool reducedExtension = false,
        int invocationCount = 1,
        bool nameofOnly = false,
        bool removeInvocationAfterFirstRun = false)
    {
        ImmutableArray<MetadataReference> references = await AnalyzerTestReferenceAssemblies.Default.ResolveAsync(language, CancellationToken.None);
        string assemblyName = scenario == "assembly" ? "Other.Hosting" : "MSTest.Extensions.Hosting";
        string apiNamespace = scenario == "namespace" ? "Other.DependencyInjection" : "Microsoft.Extensions.DependencyInjection";
        string typeName = scenario == "type" ? "OtherServiceCollectionExtensions" : "MSTestHostingServiceCollectionExtensions";
        string methodName = scenario == "method" ? "AddMSTestTestClassInjectionLookalike" : "AddMSTestTestClassInjection";
        string parameterType = scenario switch
        {
            "parameter-type" => "object",
            "parameter-interface" => "IUnrelatedCollection",
            _ => "IServiceCollection",
        };
        string consumerParameterType = scenario == "parameter-interface" ? "IUnrelatedCollection" : "IServiceCollection";
        string returnType = scenario == "return-type" ? "object" : "IServiceCollection";
        int parameterCount = scenario switch
        {
            "zero-parameters" => 0,
            "two-parameters" => 2,
            _ => 1,
        };
        string parameters = parameterCount switch
        {
            0 => string.Empty,
            2 => $"this {parameterType} services, IServiceCollection other",
            _ => $"this {parameterType} services",
        };
        // Some framework reference packs already include the DI abstractions.
        // Reuse that contract instead of introducing a second type with the same
        // metadata name in the synthetic hosting assembly.
        var referenceProbe = CSharpCompilation.Create("ContractProbe", references: references);
        string serviceContract = referenceProbe.GetTypeByMetadataName($"{apiNamespace}.IServiceCollection") is null
            ? "public interface IServiceCollection { }"
            : string.Empty;
        string apiSource = $$"""
            namespace {{apiNamespace}}
            {
                {{serviceContract}}
                public interface IUnrelatedCollection { }
                public static class {{typeName}}
                {
                    public static {{returnType}} {{methodName}}({{parameters}})
                        => default({{returnType}});
                }
            }
            """;
        var api = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(apiSource)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emittedApi = api.Emit(stream);
        Assert.IsTrue(emittedApi.Success, string.Join(Environment.NewLine, emittedApi.Diagnostics));
        PortableExecutableReference apiReference = MetadataReference.CreateFromImage(stream.ToArray());

        string arguments = string.Join(", ", Enumerable.Repeat("services", parameterCount));
        string invocation = reducedExtension
            ? $"services.{methodName}()"
            : $"{typeName}.{methodName}({arguments})";
        string body = nameofOnly
            ? $"var name = nameof({typeName}.{methodName});"
            : string.Join(Environment.NewLine, Enumerable.Repeat(invocation + ";", invocationCount));
        string source = $$"""
            using {{apiNamespace}};
            public class Registration
            {
                public void Configure({{consumerParameterType}} services)
                {
                    {{body}}
                }
            }
            """;
        Compilation compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(source)],
            references.Add(apiReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertNoCompilationErrors(compilation);

        // The shipping generator is internal. Exercise its public Roslyn contract
        // without exposing a production API or testing private implementation helpers.
        Type generatorType = typeof(ReviewAlwaysTrueAssertConditionAnalyzer).Assembly.GetType(
            "MSTest.Analyzers.MSTestHostInjectionMetadataGenerator", throwOnError: true)!;
        ISourceGenerator generator = ((IIncrementalGenerator)Activator.CreateInstance(generatorType, nonPublic: true)!).AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

        if (removeInvocationAfterFirstRun)
        {
            Assert.IsEmpty(diagnostics);
            AssertGeneratedMetadata(driver.GetRunResult(), output);
            SyntaxTree originalTree = compilation.SyntaxTrees.Single();
            SyntaxTree updatedTree = originalTree.WithChangedText(SourceText.From(source.Replace(invocation, string.Empty), Encoding.UTF8));
            compilation = compilation.ReplaceSyntaxTree(originalTree, updatedTree);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out output, out diagnostics);
        }

        Assert.IsEmpty(diagnostics);
        AssertNoCompilationErrors(output);
        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.HasCount(1, result.Results);
        Assert.IsNull(result.Results[0].Exception);
        return (result, output);
    }

    private static void AssertGeneratedMetadata(GeneratorDriverRunResult result, Compilation output)
    {
        Assert.HasCount(1, result.Results[0].GeneratedSources);
        GeneratedSourceResult generated = result.Results[0].GeneratedSources[0];
        string expectedSource = """
            // <auto-generated/>
            [assembly: System.Reflection.AssemblyMetadataAttribute("MSTestHostTestClassInjectionEnabled", "true")]
            """;
        Assert.AreEqual("MSTestHostTestClassInjectionMetadata.g.cs", generated.HintName);
        Assert.AreEqual(expectedSource.Replace("\r\n", "\n"), generated.SourceText.ToString().Replace("\r\n", "\n"));

        AttributeData[] metadata = GetHostInjectionMetadata(output);
        Assert.HasCount(1, metadata);
        Assert.AreEqual("MSTestHostTestClassInjectionEnabled", metadata[0].ConstructorArguments[0].Value);
        Assert.AreEqual("true", metadata[0].ConstructorArguments[1].Value);
    }

    private static void AssertNoMetadata(GeneratorDriverRunResult result, Compilation output)
    {
        Assert.IsEmpty(result.Results[0].GeneratedSources);
        Assert.IsEmpty(GetHostInjectionMetadata(output));
    }

    private static AttributeData[] GetHostInjectionMetadata(Compilation compilation)
        => compilation.Assembly.GetAttributes()
            .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Reflection.AssemblyMetadataAttribute")
            .ToArray();

    private static void AssertNoCompilationErrors(Compilation compilation)
    {
        Diagnostic[] errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
    }
}
