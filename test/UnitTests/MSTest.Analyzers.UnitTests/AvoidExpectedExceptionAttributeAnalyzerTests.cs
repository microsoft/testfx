// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidExpectedExceptionAttributeAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class AvoidExpectedExceptionAttributeAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public void [|TestMethod|]()
                {
                }

                [ExpectedException(typeof(System.Exception), "Some message")]
                [TestMethod]
                public void [|TestMethod2|]()
                {
                }

                [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
                [TestMethod]
                public void [|TestMethod3|]()
                {
                }

                [ExpectedException(typeof(System.Exception), "Some message", AllowDerivedTypes = true)]
                [TestMethod]
                public void [|TestMethod4|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
