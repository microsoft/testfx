// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

/// <summary>
/// Small helpers shared by the tests that exercise the source generator building blocks directly
/// (without driving a full generator run).
/// </summary>
internal static class RoslynTestHelpers
{
    /// <summary>
    /// Creates a compilation that only references the core library, which is enough to declare plain types,
    /// attributes and members without dragging the real MSTest assemblies into the test.
    /// </summary>
    public static CSharpCompilation CreateCompilation(string source, string path = "")
        => CSharpCompilation.Create(
            "TestSample",
            [CSharpSyntaxTree.ParseText(source, path: path)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    /// <summary>
    /// Finds a type declared in source by its simple name. Unlike <c>GetTypeByMetadataName</c>, this also
    /// finds file-local types, whose metadata name is mangled.
    /// </summary>
    public static INamedTypeSymbol GetSourceType(this Compilation compilation, string name)
        => compilation.GetSymbolsWithName(name, SymbolFilter.Type).OfType<INamedTypeSymbol>().Single();

    public static string NormalizeNewlines(this string value)
        => value.Replace("\r\n", "\n");
}
