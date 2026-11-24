// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

using AwesomeAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MSTest.TestFramework.SourceGeneration;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public sealed class AssertInterpolatedStringAppendMethodsGeneratorTests : TestContainer
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(CreateReferences);

    public void Generate_HandlerShapes_PreservesDeclarationsGenericParametersAndLiteralPolicy()
    {
        (string Declaration, string ExpectedDeclaration, string Parameters, string? Arguments, bool Nullable)[] cases =
        [
            ("sealed public partial class Host", "public sealed partial class Host", string.Empty, null, false),
            ("internal partial class Host", "internal partial class Host", "<TValue>", "NullableLiteralParameter = false", false),
            ("static public partial class Host", "public static partial class Host", "<TValue, TState>", "NullableLiteralParameter = true", true),
            ("abstract internal partial class Host", "internal abstract partial class Host", string.Empty, "UnrelatedOption = true", false),
        ];
        foreach ((string declaration, string expectedDeclaration, string parameters, string? arguments, bool nullable) in cases)
        {
            CSharpParseOptions options = ParseOptions(includeSpan: true);
            CSharpCompilation input = CreateCompilation(options, HandlerInput(declaration, "Handler", parameters, arguments));
            GenerationRun run = Generate(input, options);

            run.Sources.Should().ContainSingle();
            VerifySource(run.Sources[0], "Handler", expectedDeclaration, parameters, nullable);
            Emit(run.Output).Should().NotBeEmpty();
        }
    }

    public void Generate_AliasedMarker_UsesSemanticIdentity()
    {
        CSharpParseOptions options = ParseOptions(includeSpan: true);
        CSharpCompilation input = CreateCompilation(options, HandlerInput(
            "public sealed partial class Host", "Aliased", "<TItem>", "NullableLiteralParameter = true", useAlias: true));

        GenerationRun run = Generate(input, options);

        run.Sources.Should().ContainSingle();
        VerifySource(run.Sources[0], "Aliased", "public sealed partial class Host", "<TItem>", nullable: true);
        Emit(run.Output).Should().NotBeEmpty();
    }

    public void Generate_IneligibleDeclarationsOrWrongMarker_DoesNotEmit()
    {
        string[] cases =
        [
            "namespace GeneratedContracts { public partial class Host { public readonly partial struct Unmarked { } } }",
            """
            namespace Other
            {
                internal sealed class GenerateAssertInterpolatedStringAppendMethodsAttribute : System.Attribute { }
            }
            namespace GeneratedContracts
            {
                public partial class Host
                {
                    [Other.GenerateAssertInterpolatedStringAppendMethods]
                    public readonly partial struct WrongIdentity { }
                }
            }
            """,
            """
            namespace GeneratedContracts
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.GenerateAssertInterpolatedStringAppendMethods]
                public readonly partial struct TopLevel { }
            }
            """,
            """
            namespace GeneratedContracts
            {
                public partial class Host
                {
                    [Microsoft.VisualStudio.TestTools.UnitTesting.GenerateAssertInterpolatedStringAppendMethods]
                    public readonly partial record struct RecordShape;
                }
            }
            """,
        ];
        foreach (string source in cases)
        {
            CSharpParseOptions options = ParseOptions(includeSpan: true);
            CSharpCompilation input = CreateCompilation(options, source);
            GenerationRun run = Generate(input, options);

            run.Sources.Should().BeEmpty();
            run.Driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
            Emit(run.Output).Should().NotBeEmpty();
        }
    }

    public void Generate_MultipleHandlers_EmitsIndependentHintsOnce()
    {
        CSharpParseOptions options = ParseOptions(includeSpan: true);
        CSharpCompilation input = CreateCompilation(
            options,
            HandlerInput("public sealed partial class Host", "First", string.Empty, null),
            HandlerInput("public sealed partial class Host", "Second", "<TLeft, TRight>", "NullableLiteralParameter = true"),
            "namespace GeneratedContracts { public sealed partial class Host { public readonly partial struct Unmarked { } } }");

        GenerationRun run = Generate(input, options);

        run.Sources.Select(source => source.HintName).Should().BeEquivalentTo(
            new[] { "First.AppendMethods.g.cs", "Second.AppendMethods.g.cs" });
        VerifySource(
            run.Sources.Single(source => source.HintName == "First.AppendMethods.g.cs"),
            "First", "public sealed partial class Host", string.Empty, nullable: false);
        VerifySource(
            run.Sources.Single(source => source.HintName == "Second.AppendMethods.g.cs"),
            "Second", "public sealed partial class Host", "<TLeft, TRight>", nullable: true);
        Emit(run.Output).Should().NotBeEmpty();
    }

    public void Generate_FrameworkSymbols_ControlSpanOverloadCompilation()
    {
        foreach (bool includeSpan in new[] { false, true })
        {
            CSharpParseOptions options = ParseOptions(includeSpan);
            CSharpCompilation input = CreateCompilation(options, HandlerInput(
                "public sealed partial class Host", "Handler", string.Empty, null));
            GenerationRun run = Generate(input, options);
            VerifySource(run.Sources.Single(), "Handler", "public sealed partial class Host", string.Empty, nullable: false);
            INamedTypeSymbol handler = run.Output.GetTypeByMetadataName("GeneratedContracts.Host+Handler")!;
            IMethodSymbol[] appendMethods = handler.GetMembers().OfType<IMethodSymbol>()
                .Where(method => method.Name is "AppendLiteral" or "AppendFormatted").ToArray();

            appendMethods.Should().HaveCount(includeSpan ? 10 : 8);
            IMethodSymbol[] spanMethods = appendMethods.Where(method =>
                method.Parameters[0].Type is INamedTypeSymbol type
                && type.Name == "ReadOnlySpan"
                && type.ContainingNamespace.ToDisplayString() == "System").ToArray();
            spanMethods.Should().HaveCount(includeSpan ? 2 : 0);
            foreach (IMethodSymbol method in spanMethods)
            {
                ((INamedTypeSymbol)method.Parameters[0].Type).TypeArguments.Single().SpecialType.Should().Be(SpecialType.System_Char);
            }

            Emit(run.Output).Should().NotBeEmpty();
        }
    }

    public void Generate_EmittedConsumer_RendersLiteralIndependentForwarderResults()
    {
        CSharpParseOptions options = ParseOptions(includeSpan: true);
        CSharpCompilation input = CreateCompilation(options, HandlerInput(
            "public sealed partial class Host", "Handler", string.Empty, "NullableLiteralParameter = true"));
        GenerationRun run = Generate(input, options);
        const string Consumer = """
            using System;
            namespace GeneratedContracts
            {
                public static class Consumer
                {
                    public static string Render()
                    {
                        var handler = new Host.Handler();
                        handler.AppendLiteral("literal|");
                        handler.AppendFormatted(7);
                        handler.AppendLiteral("|");
                        handler.AppendFormatted(8, "D3");
                        handler.AppendLiteral("|");
                        handler.AppendFormatted(9, 4);
                        handler.AppendLiteral("|");
                        handler.AppendFormatted(10, 6, "D4");
                        handler.AppendLiteral("|");
                        handler.AppendFormatted("txt");
                        handler.AppendLiteral("|");
                        handler.AppendFormatted("xy", 4, format: null);
                        handler.AppendLiteral("|");
                        handler.AppendFormatted((object)11, 5, "D3");
                        handler.AppendLiteral("|");
                        handler.AppendFormatted("span".AsSpan());
                        handler.AppendLiteral("|");
                        handler.AppendFormatted("q".AsSpan(), 3, format: null);
                        handler.AppendLiteral(null);
                        return handler.Text;
                    }
                }
            }
            """;
        Compilation consumer = run.Output.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Consumer, options, "Consumer.cs"));
        byte[] image = Emit(consumer);
        var context = new AssemblyLoadContext("AppendMethodsConsumer", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(image);
            Assembly assembly = context.LoadFromStream(stream);
            object? rendered = assembly.GetType("GeneratedContracts.Consumer", throwOnError: true)!
                .GetMethod("Render", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            rendered.Should().Be("literal|7|008|   9|  0010|txt|  xy|  011|span|  q");
        }
        finally
        {
            context.Unload();
        }
    }

    public void Generate_ChangedMarkerOption_UpdatesSourceAndUnchangedRunStaysStable()
    {
        CSharpParseOptions options = ParseOptions(includeSpan: true);
        CSharpCompilation input = CreateCompilation(options, HandlerInput(
            "public sealed partial class Host", "Handler", string.Empty, "NullableLiteralParameter = false"));
        GenerationRun first = Generate(input, options);
        GenerationRun unchanged = Generate(input, options, first.Driver);
        VerifySource(first.Sources.Single(), "Handler", "public sealed partial class Host", string.Empty, nullable: false);
        VerifySource(unchanged.Sources.Single(), "Handler", "public sealed partial class Host", string.Empty, nullable: false);

        SyntaxTree original = input.SyntaxTrees.Single(tree => tree.FilePath == "Input0.cs");
        SyntaxTree replacement = CSharpSyntaxTree.ParseText(
            HandlerInput("public sealed partial class Host", "Handler", string.Empty, "NullableLiteralParameter = true"),
            options, original.FilePath);
        CSharpCompilation changed = input.ReplaceSyntaxTree(original, replacement);
        GenerationRun updated = Generate(changed, options, unchanged.Driver);

        updated.Sources.Should().ContainSingle();
        VerifySource(updated.Sources[0], "Handler", "public sealed partial class Host", string.Empty, nullable: true);
        Emit(updated.Output).Should().NotBeEmpty();
    }

    private static CSharpParseOptions ParseOptions(bool includeSpan)
        => new CSharpParseOptions(
            LanguageVersion.CSharp12,
            preprocessorSymbols: includeSpan ? ["NETCOREAPP3_1_OR_GREATER"] : []);

    private static CSharpCompilation CreateCompilation(CSharpParseOptions options, params string[] inputs)
    {
        IEnumerable<SyntaxTree> trees = new[] { CSharpSyntaxTree.ParseText(Support, options, "Support.cs", Encoding.UTF8) }
            .Concat(inputs.Select((source, index) => CSharpSyntaxTree.ParseText(source, options, $"Input{index}.cs", Encoding.UTF8)));
        return CSharpCompilation.Create(
            "AppendMethodsFixture_" + Guid.NewGuid().ToString("N"),
            trees,
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static GenerationRun Generate(CSharpCompilation input, CSharpParseOptions options, GeneratorDriver? driver = null)
    {
        VerifyCompilation(input);
        IIncrementalGenerator generator = new AssertInterpolatedStringAppendMethodsGenerator();
        driver ??= CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
        diagnostics.Should().BeEmpty();
        GeneratorDriverRunResult result = driver.GetRunResult();
        result.Diagnostics.Should().BeEmpty();
        result.Results.Should().ContainSingle();
        result.Results[0].Exception.Should().BeNull();
        VerifyCompilation(output);
        return new GenerationRun(driver, output, result.Results[0].GeneratedSources);
    }

    private static void VerifyCompilation(Compilation compilation)
        => compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString()).Should().BeEmpty();

    private static byte[] Emit(Compilation compilation)
    {
        VerifyCompilation(compilation);
        using var stream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        return stream.ToArray();
    }

    private static ImmutableArray<MetadataReference> CreateReferences()
    {
        string runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        string paths = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The generator tests require the current runtime's trusted platform assemblies.");
        return paths.Split(Path.PathSeparator)
            .Where(path => string.Equals(Path.GetDirectoryName(path), runtimeDirectory, StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static string HandlerInput(string host, string handler, string parameters, string? arguments, bool useAlias = false)
    {
        string marker = useAlias ? "MarkerAlias" : "Microsoft.VisualStudio.TestTools.UnitTesting.GenerateAssertInterpolatedStringAppendMethods";
        string attribute = arguments is null ? $"[{marker}]" : $"[{marker}({arguments})]";
        string alias = useAlias
            ? "using MarkerAlias = Microsoft.VisualStudio.TestTools.UnitTesting.GenerateAssertInterpolatedStringAppendMethodsAttribute;"
            : string.Empty;
        return $$"""
            #nullable enable
            using System.Text;
            {{alias}}
            namespace GeneratedContracts
            {
                {{host}}
                {
                    {{attribute}}
                    public readonly partial struct {{handler}}{{parameters}}
                    {
                        private readonly StringBuilder? _builder;
                        public {{handler}}() { _builder = new StringBuilder(); }
                        public string Text => _builder?.ToString() ?? string.Empty;
                    }
                }
            }
            """;
    }

    private static void VerifySource(GeneratedSourceResult source, string handler, string host, string parameters, bool nullable)
    {
        source.HintName.Should().Be($"{handler}.AppendMethods.g.cs");
        source.SourceText.Encoding!.WebName.Should().Be("utf-8");
        string literalType = nullable ? "string?" : "string";
        string expected = $$"""
            // <auto-generated/>
            // Copyright (c) Microsoft Corporation. All rights reserved.
            // Licensed under the MIT license. See LICENSE file in the project root for full license information.
            #nullable enable

            using System;

            #pragma warning disable CS1591
            #pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
            #pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

            namespace GeneratedContracts;

            {{host}}
            {
                public readonly partial struct {{handler}}{{parameters}}
                {
                    public void AppendLiteral({{literalType}} value) => AssertInterpolatedStringHandlerAppender.AppendLiteral(_builder!, value);

                    public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

            #if NETCOREAPP3_1_OR_GREATER
                    public void AppendFormatted(ReadOnlySpan<char> value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

                    public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
            #endif

                    public void AppendFormatted<T>(T value, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, format);

                    public void AppendFormatted<T>(T value, int alignment) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment);

                    public void AppendFormatted<T>(T value, int alignment, string? format) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

                    public void AppendFormatted(string? value) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value);

                    public void AppendFormatted(string? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);

                    public void AppendFormatted(object? value, int alignment = 0, string? format = null) => AssertInterpolatedStringHandlerAppender.AppendFormatted(_builder!, value, alignment, format);
                }
            }
            """ + "\n";
        source.SourceText.ToString().Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
    }

    private const string Support = """
        #nullable enable
        using System;
        using System.Globalization;
        using System.Text;
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            [AttributeUsage(AttributeTargets.Struct)]
            internal sealed class GenerateAssertInterpolatedStringAppendMethodsAttribute : Attribute
            {
                public bool NullableLiteralParameter { get; set; }
                public bool UnrelatedOption { get; set; }
            }
        }
        namespace GeneratedContracts
        {
            internal static class AssertInterpolatedStringHandlerAppender
            {
                public static void AppendLiteral(StringBuilder builder, string? value) => builder.Append(value);
                public static void AppendFormatted<T>(StringBuilder builder, T value, string? format) => Append(builder, value, 0, format);
                public static void AppendFormatted<T>(StringBuilder builder, T value, int alignment) => Append(builder, value, alignment, null);
                public static void AppendFormatted<T>(StringBuilder builder, T value, int alignment, string? format) => Append(builder, value, alignment, format);
                public static void AppendFormatted(StringBuilder builder, string? value) => builder.Append(value);
                public static void AppendFormatted(StringBuilder builder, string? value, int alignment = 0, string? format = null) => Append(builder, value, alignment, format);
                public static void AppendFormatted(StringBuilder builder, object? value, int alignment = 0, string? format = null) => Append(builder, value, alignment, format);
                public static void AppendFormatted(StringBuilder builder, ReadOnlySpan<char> value) => builder.Append(value);
                private static void Append(StringBuilder builder, object? value, int alignment, string? format)
                    => builder.AppendFormat(CultureInfo.InvariantCulture, "{0," + alignment + (format is null ? "" : ":" + format) + "}", value);
            }
        }
        """;

    private sealed record GenerationRun(GeneratorDriver Driver, Compilation Output, ImmutableArray<GeneratedSourceResult> Sources);
}
