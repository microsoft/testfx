// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestPropertyAttributeOnTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class TestPropertyAttributeOnTestMethodAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenMethodIsMarkedWithTestMethodAndTestPropertyAttributes_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [TestProperty("name", "value")]
                public void TestMethod()
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsMarkedWithTestPropertyAttributeButNotWithTestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:TestProperty("name", "value")|}]
                public void TestMethod()
                {
                }
            }
            """
        ;
        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    public async Task WhenMethodIsMarkedWithTestPropertyAttributeAndCustomTestMethod_NoDiagnostic()
    {
        var code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestProperty("name", "value")]
                [MyCustomTestMethod]
                public void TestMethod()
                {
                }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class MyCustomTestMethodAttribute : TestMethodAttribute
            {
            }
            """
        ;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
