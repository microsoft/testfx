// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PriorityAttributeOnTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PriorityAttributeOnTestMethodAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenMethodIsMarkedWithTestMethodAndPriorityAttributes_NoDiagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Priority(1)]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenMethodIsMarkedWithPriorityAttributeButNotWithTestMethod_Diagnostic()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [{|#0:Priority(1)|}]
                public void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    public async Task WhenMethodIsMarkedWithPriorityAttributeAndCustomTestMethod_NoDiagnostic()
    {
        var code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Priority(1)]
                [MyCustomTestMethod]
                public void TestMethod()
                {
                }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class MyCustomTestMethodAttribute : TestMethodAttribute
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
