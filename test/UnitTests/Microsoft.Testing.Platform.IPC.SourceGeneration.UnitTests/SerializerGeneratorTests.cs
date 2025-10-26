// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Testing.Framework;

namespace Microsoft.Testing.Platform.IPC.SourceGeneration.UnitTests;

[TestClass]
public sealed class SerializerGeneratorTests
{
    [TestMethod]
    public async Task GenerateSerializer_SimpleRequest_GeneratesSerializer()
    {
        // Arrange
        string source = """
            using Microsoft.Testing.Platform.IPC;
            
            namespace TestNamespace;
            
            [GenerateSerializer(1)]
            internal sealed record SimpleRequest(int Value) : IRequest;
            """;

        // Act
        var (compilation, diagnostics) = await RunGeneratorAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
        compilation.SyntaxTrees.Should().Contain(tree => tree.FilePath.EndsWith("SimpleRequestSerializer.g.cs"));
        
        var generatedTree = compilation.SyntaxTrees.First(tree => tree.FilePath.EndsWith("SimpleRequestSerializer.g.cs"));
        string generatedCode = generatedTree.ToString();
        
        generatedCode.Should().Contain("class SimpleRequestSerializer");
        generatedCode.Should().Contain("public int Id => 1;");
        generatedCode.Should().Contain("ReadInt(stream)");
        generatedCode.Should().Contain("WriteInt(stream, request.Value)");
    }

    [TestMethod]
    public async Task GenerateSerializer_EmptyRequest_GeneratesSerializer()
    {
        // Arrange
        string source = """
            using Microsoft.Testing.Platform.IPC;
            
            namespace TestNamespace;
            
            [GenerateSerializer(2)]
            internal sealed record EmptyRequest : IRequest;
            """;

        // Act
        var (compilation, diagnostics) = await RunGeneratorAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
        
        var generatedTree = compilation.SyntaxTrees.First(tree => tree.FilePath.EndsWith("EmptyRequestSerializer.g.cs"));
        string generatedCode = generatedTree.ToString();
        
        generatedCode.Should().Contain("return new EmptyRequest();");
        generatedCode.Should().Contain("// Empty request/response - nothing to serialize");
    }

    [TestMethod]
    public async Task GenerateSerializer_MultipleFields_GeneratesSerializerInOrder()
    {
        // Arrange
        string source = """
            using Microsoft.Testing.Platform.IPC;
            
            namespace TestNamespace;
            
            [GenerateSerializer(3)]
            internal sealed record MultiFieldRequest(
                string Name,
                int Age,
                bool IsActive) : IRequest;
            """;

        // Act
        var (compilation, diagnostics) = await RunGeneratorAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
        
        var generatedTree = compilation.SyntaxTrees.First(tree => tree.FilePath.EndsWith("MultiFieldRequestSerializer.g.cs"));
        string generatedCode = generatedTree.ToString();
        
        // Verify read order
        generatedCode.Should().Contain("ReadString(stream)");
        generatedCode.Should().Contain("ReadInt(stream)");
        generatedCode.Should().Contain("ReadBool(stream)");
        
        // Verify write order matches
        int nameIndex = generatedCode.IndexOf("WriteString(stream, request.Name");
        int ageIndex = generatedCode.IndexOf("WriteInt(stream, request.Age");
        int activeIndex = generatedCode.IndexOf("WriteBool(stream, request.IsActive");
        
        nameIndex.Should().BeLessThan(ageIndex);
        ageIndex.Should().BeLessThan(activeIndex);
    }

    [TestMethod]
    public async Task GenerateSerializer_ComplexType_GeneratesStub()
    {
        // Arrange
        string source = """
            using Microsoft.Testing.Platform.IPC;
            
            namespace TestNamespace;
            
            public class NestedObject { }
            
            [GenerateSerializer(4)]
            internal sealed record ComplexRequest(
                string Name,
                NestedObject[] Items) : IRequest;
            """;

        // Act
        var (compilation, diagnostics) = await RunGeneratorAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
        
        var generatedTree = compilation.SyntaxTrees.First(tree => tree.FilePath.EndsWith("ComplexRequestSerializer.g.cs"));
        string generatedCode = generatedTree.ToString();
        
        generatedCode.Should().Contain("NotImplementedException");
        generatedCode.Should().Contain("TODO");
    }

    [TestMethod]
    public async Task GenerateSerializer_Response_GeneratesSerializer()
    {
        // Arrange
        string source = """
            using Microsoft.Testing.Platform.IPC;
            
            namespace TestNamespace;
            
            [GenerateSerializer(5)]
            internal sealed record SimpleResponse(bool Success) : IResponse;
            """;

        // Act
        var (compilation, diagnostics) = await RunGeneratorAsync(source);

        // Assert
        diagnostics.Should().BeEmpty();
        compilation.SyntaxTrees.Should().Contain(tree => tree.FilePath.EndsWith("SimpleResponseSerializer.g.cs"));
    }

    private static async Task<(Compilation, ImmutableArray<Diagnostic>)> RunGeneratorAsync(string source)
    {
        // Create a compilation with the source
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Stream).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new SerializerGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var runResult = driver.GetRunResult();
        var outputCompilation = runResult.GeneratedTrees.Any() 
            ? compilation.AddSyntaxTrees(runResult.GeneratedTrees)
            : compilation;

        return (outputCompilation, runResult.Diagnostics);
    }
}
