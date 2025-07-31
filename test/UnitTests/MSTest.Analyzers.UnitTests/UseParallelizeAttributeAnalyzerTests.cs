// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

using MSTest.TestAdapter;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseParallelizeAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public class UseParallelizeAttributeAnalyzerTests
{
    private static async Task VerifyAsync(string code, bool includeTestAdapter, params DiagnosticResult[] expected)
    {
        var test = new VerifyCS.Test
        {
            TestCode = code,
        };

        if (includeTestAdapter)
        {
            // NOTE: Test constructor already adds TestFramework refs.
#pragma warning disable CS0618 // Type or member is obsolete
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(MSTestExecutor).Assembly.Location));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    [TestMethod]
    public async Task WhenNoAttributeSpecified_TestAdapterNotReferenced_NoDiagnostic()
        => await VerifyAsync(string.Empty, includeTestAdapter: false);

    [TestMethod]
    public async Task WhenNoAttributeSpecified_TestAdapterReferenced_Diagnostic()
        => await VerifyAsync(string.Empty, includeTestAdapter: true, VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.Rule).WithNoLocation());

    [TestMethod]
    public async Task WhenParallelizeAttributeSet_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
            """;

        await VerifyAsync(code, includeTestAdapter: true);
        await VerifyAsync(code, includeTestAdapter: false);
    }

    [TestMethod]
    public async Task WhenDoNotParallelizeAttributeSet_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DoNotParallelize]
            """;

        await VerifyAsync(code, includeTestAdapter: true);
        await VerifyAsync(code, includeTestAdapter: false);
    }
}
