// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

if (args.Length != 2)
{
    throw new ArgumentException("Expected the extracted analyzer package and the C# consumer source.");
}

string packageDirectory = Path.GetFullPath(args[0]);
Version expectedApi = new(3, 11, 0, 0);
Assert.AreEqual(expectedApi, typeof(Compilation).Assembly.GetName().Version);
Assert.AreEqual(expectedApi, typeof(CSharpCompilation).Assembly.GetName().Version);
Assert.AreEqual(expectedApi, typeof(Workspace).Assembly.GetName().Version);

Assembly analyzerAssembly = LoadPackageAssembly("MSTest.Analyzers");
Assembly fixesAssembly = LoadPackageAssembly("MSTest.Analyzers.CodeFixes");
Type[] analyzerTypes = analyzerAssembly.GetExportedTypes();
Type[] fixerTypes = fixesAssembly.GetExportedTypes();
Assert.IsGreaterThan(0, analyzerTypes.Count(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type)));
Assert.IsGreaterThan(0, fixerTypes.Count(type => typeof(CodeFixProvider).IsAssignableFrom(type)));

if (Activator.CreateInstance(analyzerAssembly.GetType("MSTest.Analyzers.TypeContainingTestMethodShouldBeATestClassAnalyzer", throwOnError: true)!) is not DiagnosticAnalyzer analyzer
    || Activator.CreateInstance(fixesAssembly.GetType("MSTest.Analyzers.AddTestClassFixer", throwOnError: true)!) is not CodeFixProvider fixer)
{
    throw new InvalidOperationException("The package did not supply the expected analyzer and code fix.");
}

DiagnosticDescriptor rule = analyzer.SupportedDiagnostics.Single();
Assert.AreEqual("MSTEST0030", rule.Id);
CultureInfo french = CultureInfo.GetCultureInfo("fr");
string englishTitle = rule.Title.ToString(CultureInfo.GetCultureInfo("en"));
string frenchTitle = rule.Title.ToString(french);

string trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
    ?? throw new InvalidOperationException("The consumer runtime did not supply platform references.");
IEnumerable<MetadataReference> references = trustedAssemblies.Split(Path.PathSeparator)
    .Append(typeof(TestClassAttribute).Assembly.Location)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Select(path => MetadataReference.CreateFromFile(path));

using var workspace = new AdhocWorkspace();
Project project = workspace.AddProject("PackageConsumer", LanguageNames.CSharp)
    .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    .WithMetadataReferences(references);
Document document = project.AddDocument("MissingTestClass.cs", SourceText.From(File.ReadAllText(args[1])));
Compilation compilation = await GetCompilationAsync(document);
ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAnalyzerDiagnosticsAsync();
Assert.HasCount(1, diagnostics);
Assert.AreEqual("MSTEST0030", diagnostics[0].Id);
Assert.AreEqual(DiagnosticSeverity.Warning, diagnostics[0].Severity);

List<CodeAction> actions = [];
CultureInfo previousCulture = CultureInfo.CurrentUICulture;
try
{
    CultureInfo.CurrentUICulture = french;
    await fixer.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostics[0], (action, _) => actions.Add(action), CancellationToken.None));
}
finally
{
    CultureInfo.CurrentUICulture = previousCulture;
}

Assert.HasCount(1, actions);
ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(CancellationToken.None);
ApplyChangesOperation change = operations.OfType<ApplyChangesOperation>().Single();
Document fixedDocument = change.ChangedSolution.GetDocument(document.Id)
    ?? throw new InvalidOperationException("The code fix removed the consumer document.");
SyntaxNode root = await fixedDocument.GetSyntaxRootAsync()
    ?? throw new InvalidOperationException("The code fix produced no syntax root.");
string expected = """
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [assembly: DoNotParallelize]
    [TestClass]
    public class MissingTestClass
    {
        [TestMethod]
        public void Test()
        {
        }
    }
    """;
Assert.AreEqual(Normalize(expected), Normalize(root.NormalizeWhitespace().ToFullString()));
Compilation fixedCompilation = await GetCompilationAsync(fixedDocument);
Assert.IsEmpty(await fixedCompilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAnalyzerDiagnosticsAsync());

Console.WriteLine(JsonSerializer.Serialize(new
{
    RoslynVersion = typeof(Compilation).Assembly.GetName().Version!.ToString(),
    AnalyzerPath = analyzerAssembly.Location,
    AnalyzerVersion = analyzerAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    CodeFixPath = fixesAssembly.Location,
    CodeFixVersion = fixesAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    AnalyzerTypeCount = analyzerTypes.Count(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type)),
    CodeFixTypeCount = fixerTypes.Count(type => typeof(CodeFixProvider).IsAssignableFrom(type)),
    DiagnosticId = diagnostics[0].Id,
    EnglishTitle = englishTitle,
    FrenchTitle = frenchTitle,
    FrenchCodeFix = actions[0].Title,
    FixedSource = Normalize(root.NormalizeWhitespace().ToFullString()),
}));

Assembly LoadPackageAssembly(string name)
{
    string path = Path.Combine(packageDirectory, "analyzers", "dotnet", "cs", name + ".dll");
    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    Assert.AreEqual(path, assembly.Location);
    Assert.AreEqual(".NETStandard,Version=v2.0", assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
    AssemblyName[] roslynReferences = assembly.GetReferencedAssemblies()
        .Where(reference => reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
        .ToArray();
    Assert.IsGreaterThan(0, roslynReferences.Length);
    foreach (AssemblyName reference in roslynReferences)
    {
        Assert.AreEqual(expectedApi, reference.Version, reference.FullName);
    }

    return assembly;
}

static async Task<Compilation> GetCompilationAsync(Document document)
{
    Compilation compilation = await document.Project.GetCompilationAsync()
        ?? throw new InvalidOperationException("The package consumer did not produce a compilation.");
    Assert.IsEmpty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray());
    return compilation;
}

static string Normalize(string text)
    => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
