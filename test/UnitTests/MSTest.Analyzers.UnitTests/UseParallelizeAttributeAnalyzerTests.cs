// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

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
            test.TestState.AnalyzerConfigFiles.Add((
                "/.globalconfig",
                """
                is_global = true

                build_property.IsMSTestTestAdapterReferenced = true
                """));
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

    [TestMethod]
    public async Task WhenBothAttributesSet_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
            [assembly: DoNotParallelize]
            """;

        await VerifyAsync(code, includeTestAdapter: true, VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.DoNotUseBothAttributesRule).WithNoLocation());
        await VerifyAsync(code, includeTestAdapter: false, VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.DoNotUseBothAttributesRule).WithNoLocation());
    }

    [TestMethod]
    public async Task WhenBothAttributesSetInDifferentOrder_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DoNotParallelize]
            [assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
            """;

        await VerifyAsync(code, includeTestAdapter: true, VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.DoNotUseBothAttributesRule).WithNoLocation());
        await VerifyAsync(code, includeTestAdapter: false, VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.DoNotUseBothAttributesRule).WithNoLocation());
    }
}
