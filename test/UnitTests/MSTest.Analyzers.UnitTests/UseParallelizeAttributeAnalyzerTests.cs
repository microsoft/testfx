// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseParallelizeAttributeAnalyzer,
    MSTest.Analyzers.UseParallelizeAttributeFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public class UseParallelizeAttributeAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenNoAttributeSpecified_Diagnostic()
    {
        await VerifyCS.VerifyAnalyzerAsync(
            string.Empty,
            VerifyCS.Diagnostic(UseParallelizeAttributeAnalyzer.Rule).WithNoLocation());
    }

    public async Task WhenParallelizeAttributeSet_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenDoNotParallelizeAttributeSet_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [assembly: DoNotParallelize]
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
