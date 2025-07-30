// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using FluentAssertions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.Testing.Extensions;
using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests.TestUtilities;

internal sealed class GeneratorTester
{
    private readonly Func<IIncrementalGenerator> _incrementalGeneratorFactory;
    private readonly string[] _additionalReferences;
    private static readonly SemaphoreSlim Lock = new(1);

    public GeneratorTester(Func<IIncrementalGenerator> incrementalGeneratorFactory, string[] additionalReferences)
    {
        _incrementalGeneratorFactory = incrementalGeneratorFactory;
        _additionalReferences = additionalReferences;
    }

    public static GeneratorTester TestGraph { get; } =
        new(
            () => new TestNodesGenerator(),
            [
                // Microsoft.Testing.Platform dll
                Assembly.GetAssembly(typeof(IProperty))!.Location,

                // Microsoft.Testing.Framework dll
                Assembly.GetAssembly(typeof(TestNode))!.Location,

                // Microsoft.Testing.Extensions dll
                Assembly.GetAssembly(typeof(TrxReportExtensions))!.Location,

                // Microsoft.Testing.Extensions.TrxReport.Abstractions dll
                Assembly.GetAssembly(typeof(TrxExceptionProperty))!.Location,

                // MSTest.TestFramework  dll
                Assembly.GetAssembly(typeof(TestClassAttribute))!.Location
            ]);

    public static ImmutableArray<MetadataReference>? Net80MetadataReferences { get; set; }

    public async Task<GeneratorCompilationResult> CompileAndExecuteAsync(string source, CancellationToken cancellationToken)
        => await CompileAndExecuteAsync([source], cancellationToken);

    public async Task<GeneratorCompilationResult> CompileAndExecuteAsync(string[] sources, CancellationToken cancellationToken)
    {
        // Cache the resolution in local and try to fire the finalizers
        // In CI sometime we have a crash for http connection and the suspect is
        // this call below that connects to nuget.org
        if (Net80MetadataReferences is null)
        {
            await Lock.WaitAsync(cancellationToken);
            try
            {
                if (Net80MetadataReferences is null)
                {
                    Net80MetadataReferences =
                        await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, cancellationToken);

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
            }
            finally
            {
                Lock.Release();
            }
        }

        MetadataReference[] metadataReferences = [.. Net80MetadataReferences.Value, .. _additionalReferences.Select(loc => MetadataReference.CreateFromFile(loc))];

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            sources.Select(source => CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken)),
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ISourceGenerator generator = _incrementalGeneratorFactory().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
           generators: [generator]);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation? outputCompilation,
            out ImmutableArray<Diagnostic> diagnostics, cancellationToken);
        diagnostics.Should().BeEmpty();

        using var ms = new MemoryStream();
        EmitResult result = outputCompilation.Emit(ms, cancellationToken: cancellationToken);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        GeneratorDriverTimingInfo timingInfo = driver.GetTimingInfo();

        if (result.Success)
        {
            return new(runResult, timingInfo, result, null);
        }

        var code = new StringBuilder();

        // Append diagnostics that are not tied to any file.
        foreach (Diagnostic? globalDiagnostic in result.Diagnostics.Where(d => d.Location.SourceTree == null || string.IsNullOrWhiteSpace(d.Location.SourceTree.FilePath)))
        {
            code.AppendLine(globalDiagnostic.ToString());
        }

        foreach (SyntaxTree output in outputCompilation.SyntaxTrees)
        {
            IEnumerable<Diagnostic> d = output.GetDiagnostics(cancellationToken);

            var diagnosticsByLine = new Dictionary<int, List<Diagnostic>>();
            result.Diagnostics
                .Where(d => !string.IsNullOrEmpty(output.FilePath) && d.Location.SourceTree?.FilePath == output.FilePath)
                .GroupBy(d => d.Location.GetLineSpan().StartLinePosition)
                .ToList()
                .ForEach(f =>
                {
                    if (diagnosticsByLine.TryGetValue(f.Key.Line, out List<Diagnostic>? list))
                    {
                        list.AddRange(f);
                    }
                    else
                    {
                        var l = new List<Diagnostic>();
                        l.AddRange(f);
                        diagnosticsByLine[f.Key.Line] = l;
                    }
                });

            if (diagnosticsByLine.Count == 0)
            {
                continue;
            }

            code.Append("file '").Append(output.FilePath).AppendLine("':");
            string[] lines = output.ToString().Split('\n');
            int length = lines.Length;
            int pad = length.ToString(CultureInfo.InvariantCulture).Length;
            for (int i = 0; i < length; i++)
            {
                if (diagnosticsByLine.TryGetValue(i, out List<Diagnostic>? diagnosticsForLine))
                {
                    code.AppendLine();
                    foreach (Diagnostic diagnostic in diagnosticsForLine)
                    {
                        code.Append(">>> ").AppendLine(diagnostic.ToString());
                    }
                }

                // Add line number (starting from 1)
                code.Append((i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0'));
                code.Append(' ').AppendLine(lines[i]);
            }

            code.AppendLine();
        }

        return new(runResult, timingInfo, result, code.ToString());
    }
}
