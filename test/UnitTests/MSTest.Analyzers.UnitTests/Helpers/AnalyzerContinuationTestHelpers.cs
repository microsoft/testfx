// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace MSTest.Analyzers.Test;

internal static class AnalyzerContinuationTestHelpers
{
    internal static async Task<Document> CreateDocumentAsync(
        AdhocWorkspace workspace,
        string source,
        bool includeMSTest = true,
        IEnumerable<MetadataReference>? additionalReferences = null,
        CSharpCompilationOptions? options = null)
    {
        ImmutableArray<MetadataReference> references = await AnalyzerTestReferenceAssemblies.Default.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        if (includeMSTest)
        {
            references = references.AddRange(
                new[] { typeof(TestClassAttribute).Assembly.Location, typeof(TestContext).Assembly.Location }
                    .Distinct(StringComparer.Ordinal)
                    .Select(path => MetadataReference.CreateFromFile(path)));
        }

        Project project = workspace.AddProject(ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "ContinuationFixture",
            "ContinuationFixture",
            LanguageNames.CSharp,
            compilationOptions: options ?? new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true),
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            metadataReferences: additionalReferences is null ? references : references.AddRange(additionalReferences)));
        return workspace.AddDocument(project.Id, "Fixture.cs", SourceText.From(source, Encoding.UTF8));
    }

    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        Document document,
        DiagnosticAnalyzer analyzer,
        AnalyzerOptions? analyzerOptions = null,
        bool enableDisabledRules = false,
        SyntaxTreeOptionsProvider? treeOptionsProvider = null,
        params string[] expectedCompilerErrors)
    {
        Compilation compilation = (await document.Project.GetCompilationAsync())!;
        if (treeOptionsProvider is not null)
        {
            // Workspace compilations install their own provider from project analyzer configs.
            // Model the compiler's per-tree configuration on the final compilation.
            compilation = compilation.WithOptions(compilation.Options.WithSyntaxTreeOptionsProvider(treeOptionsProvider));
        }

        AssertCompilerErrors(compilation, expectedCompilerErrors);
        if (enableDisabledRules)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                compilation.Options.SpecificDiagnosticOptions.SetItems(
                    analyzer.SupportedDiagnostics.Select(rule => new KeyValuePair<string, ReportDiagnostic>(rule.Id, ReportDiagnostic.Info)))));
        }

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(
            [analyzer],
            new CompilationWithAnalyzersOptions(
                analyzerOptions ?? new AnalyzerOptions([]),
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false)).GetAnalyzerDiagnosticsAsync();
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Id == "AD0001"), string.Join(Environment.NewLine, diagnostics));
        return [.. diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)];
    }

    internal static void AssertCompilerErrors(Compilation compilation, params string[] expectedIds)
    {
        string[] actualIds = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(
            expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            actualIds,
            string.Join(Environment.NewLine, compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
    }

    internal static async Task<List<CodeAction>> GetActionsAsync(Document document, Diagnostic diagnostic, CodeFixProvider fixer)
    {
        var actions = new List<CodeAction>();
        await fixer.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None));
        return actions;
    }

    internal static async Task<Document> ApplyAsync(Document document, CodeAction action)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation[] changes = operations.OfType<ApplyChangesOperation>().ToArray();
        Assert.HasCount(1, changes);
        Document changed = changes[0].ChangedSolution.GetDocument(document.Id)!;
        changed = await Simplifier.ReduceAsync(changed);
        return await Formatter.FormatAsync(changed);
    }

    internal static async Task AssertSourceAsync(Document document, string expected, params string[] expectedCompilerErrors)
    {
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        SyntaxNode expectedRoot = CSharpSyntaxTree.ParseText(expected, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
        Assert.AreEqual(expectedRoot.NormalizeWhitespace().ToFullString(), root.NormalizeWhitespace().ToFullString());
        AssertCompilerErrors((await document.Project.GetCompilationAsync())!, expectedCompilerErrors);
    }

    internal static void AssertDiagnostic(Diagnostic diagnostic, string id, DiagnosticSeverity severity, string source, string span, string message)
    {
        Assert.AreEqual(id, diagnostic.Id);
        Assert.AreEqual(severity, diagnostic.Severity);
        Assert.AreEqual(span, source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
        Assert.AreEqual(message, diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    internal static async Task<MetadataReference> EmitReferenceAsync(string source, string name, string? alias = null)
    {
        ImmutableArray<MetadataReference> references = await AnalyzerTestReferenceAssemblies.Default.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        AssertCompilerErrors(compilation);
        using var stream = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(
            stream.ToArray(),
            alias is null ? MetadataReferenceProperties.Assembly : MetadataReferenceProperties.Assembly.WithAliases([alias]));
    }
}
