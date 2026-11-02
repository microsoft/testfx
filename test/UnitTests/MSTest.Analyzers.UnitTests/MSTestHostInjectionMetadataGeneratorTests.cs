// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class MSTestHostInjectionMetadataGeneratorTests
{
    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    [DataRow(LanguageNames.VisualBasic)]
    public async Task Generate_StaticInvocation_EmitsMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language);

        AssertGeneratedMetadata(result, output, language);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    [DataRow(LanguageNames.VisualBasic)]
    public async Task Generate_ReducedExtensionInvocation_EmitsMetadata(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, reducedExtension: true);

        AssertGeneratedMetadata(result, output, language);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    [DataRow(LanguageNames.VisualBasic)]
    public async Task Generate_RepeatedInvocations_EmitsMetadataOnce(string language)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, invocationCount: 3);

        AssertGeneratedMetadata(result, output, language);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp, "assembly")]
    [DataRow(LanguageNames.VisualBasic, "assembly")]
    [DataRow(LanguageNames.CSharp, "namespace")]
    [DataRow(LanguageNames.VisualBasic, "namespace")]
    [DataRow(LanguageNames.CSharp, "type")]
    [DataRow(LanguageNames.VisualBasic, "type")]
    [DataRow(LanguageNames.CSharp, "method")]
    [DataRow(LanguageNames.VisualBasic, "method")]
    [DataRow(LanguageNames.CSharp, "zero-parameters")]
    [DataRow(LanguageNames.VisualBasic, "zero-parameters")]
    [DataRow(LanguageNames.CSharp, "two-parameters")]
    [DataRow(LanguageNames.VisualBasic, "two-parameters")]
    [DataRow(LanguageNames.CSharp, "parameter-type")]
    [DataRow(LanguageNames.VisualBasic, "parameter-type")]
    [DataRow(LanguageNames.CSharp, "parameter-interface")]
    [DataRow(LanguageNames.VisualBasic, "parameter-interface")]
    [DataRow(LanguageNames.CSharp, "return-type")]
    [DataRow(LanguageNames.VisualBasic, "return-type")]
    public async Task Generate_LookalikeApi_DoesNotEmitMetadata(string language, string scenario)
    {
        (GeneratorDriverRunResult result, Compilation output) = await RunGeneratorAsync(language, scenario);

        AssertNoMetadata(result, output);
    }

    [TestMethod]
    [DataRow(LanguageNames.CSharp)]
    [DataRow(LanguageNames.VisualBasic)]
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
        bool nameofOnly = false)
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
            : string.Join(Environment.NewLine, Enumerable.Repeat(invocation + (language == LanguageNames.CSharp ? ";" : string.Empty), invocationCount));
        string source = language == LanguageNames.CSharp
            ? $$"""
                using {{apiNamespace}};
                public class Registration
                {
                    public void Configure({{consumerParameterType}} services)
                    {
                        {{body}}
                    }
                }
                """
            : $$"""
                Imports {{apiNamespace}}
                Public Class Registration
                    Public Sub Configure(services As {{consumerParameterType}})
                        {{body}}
                    End Sub
                End Class
                """;
        Compilation compilation = language == LanguageNames.CSharp
            ? CSharpCompilation.Create(
                "Consumer",
                [CSharpSyntaxTree.ParseText(source)],
                references.Add(apiReference),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            : VisualBasicCompilation.Create(
                "Consumer",
                [VisualBasicSyntaxTree.ParseText(source)],
                references.Add(apiReference),
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertNoCompilationErrors(compilation);

        // The shipping generator is internal. Exercise its public Roslyn contract
        // without exposing a production API or testing private implementation helpers.
        Type generatorType = typeof(ReviewAlwaysTrueAssertConditionAnalyzer).Assembly.GetType(
            "MSTest.Analyzers.MSTestHostInjectionMetadataGenerator", throwOnError: true)!;
        var generator = (ISourceGenerator)Activator.CreateInstance(generatorType, nonPublic: true)!;
        GeneratorDriver driver = language == LanguageNames.CSharp
            ? CSharpGeneratorDriver.Create(generator)
            : VisualBasicGeneratorDriver.Create(ImmutableArray.Create(generator));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

        Assert.IsEmpty(diagnostics);
        AssertNoCompilationErrors(output);
        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.HasCount(1, result.Results);
        Assert.IsNull(result.Results[0].Exception);
        return (result, output);
    }

    private static void AssertGeneratedMetadata(GeneratorDriverRunResult result, Compilation output, string language)
    {
        Assert.HasCount(1, result.Results[0].GeneratedSources);
        GeneratedSourceResult generated = result.Results[0].GeneratedSources[0];
        string expectedSource = language == LanguageNames.CSharp
            ? """
                // <auto-generated/>
                [assembly: System.Reflection.AssemblyMetadataAttribute("MSTestHostTestClassInjectionEnabled", "true")]
                """
            : """
                ' <auto-generated/>
                <Assembly: System.Reflection.AssemblyMetadataAttribute("MSTestHostTestClassInjectionEnabled", "true")>
                """;
        string extension = language == LanguageNames.CSharp ? "cs" : "vb";
        Assert.AreEqual($"MSTestHostTestClassInjectionMetadata.g.{extension}", generated.HintName);
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
