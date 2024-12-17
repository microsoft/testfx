// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseParallelizeAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public class UseParallelizeAttributeAnalyzerTests
{
    [TestMethod]
    public async Task WhenNoAttributeSpecified_Diagnostic() => await VerifyCS.VerifyAnalyzerAsync(
            string.Empty,
            VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.Rule).WithNoLocation());

    [TestMethod]
    public async Task WhenParallelizeAttributeSet_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDoNotParallelizeAttributeSet_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DoNotParallelize]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
