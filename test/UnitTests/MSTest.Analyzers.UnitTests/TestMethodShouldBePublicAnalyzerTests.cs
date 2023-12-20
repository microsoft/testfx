// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldBePublicAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestMethodShouldBePublicAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenMethodIsPublicAndTestMethod_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenMethodIsNotPublicAndTestMethod_Diagnostic(string accessibility)
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                {{accessibility}} void [|MyTestMethod|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsNotPublicAndNotTestMethod_NoDiagnostic()
    {
        var code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void PrivateMethod()
                {
                }

                protected void ProtectedMethod()
                {
                }

                internal protected void InternalProtectedMethod()
                {
                }

                internal void InternalMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
