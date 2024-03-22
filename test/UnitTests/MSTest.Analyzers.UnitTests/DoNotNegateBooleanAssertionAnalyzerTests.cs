// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotNegateBooleanAssertionAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class DoNotNegateBooleanAssertionAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssertionIsNotNegated_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool b = true;

                    Assert.IsTrue(true);
                    Assert.IsTrue(false);
                    Assert.IsTrue(b);
                    Assert.IsTrue(GetBoolean());

                    Assert.IsFalse(true);
                    Assert.IsFalse(false);
                    Assert.IsFalse(b);
                    Assert.IsFalse(GetBoolean());
                }

                private bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssertionIsNegated_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool b = true;
            
                    [|Assert.IsTrue(!true)|];
                    [|Assert.IsTrue(!false)|];
                    [|Assert.IsTrue(!b)|];
                    [|Assert.IsTrue(!GetBoolean())|];
                    [|Assert.IsTrue(!(GetBoolean()))|];
                    [|Assert.IsTrue((!(GetBoolean())))|];

                    [|Assert.IsFalse(!true)|];
                    [|Assert.IsFalse(!false)|];
                    [|Assert.IsFalse(!b)|];
                    [|Assert.IsFalse(!GetBoolean())|];
                    [|Assert.IsFalse(!(GetBoolean()))|];
                    [|Assert.IsFalse((!(GetBoolean())))|];
                }

                private bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
