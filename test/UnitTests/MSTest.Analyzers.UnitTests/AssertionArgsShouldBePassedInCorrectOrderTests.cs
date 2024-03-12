// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertionArgsShouldBePassedInCorrectOrder,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class AssertionArgsShouldBePassedInCorrectOrderTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    private async Task WhenUsingLiterals()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;

                    [|Assert.AreEqual(s, "")|];
                    [|Assert.AreEqual(b, true)|];
                    [|Assert.AreEqual(i, 1)|];

                    [|Assert.AreSame(s, "")|];
                    [|Assert.AreSame(b, true)|];
                    [|Assert.AreSame(i, 1)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;
            
                    Assert.AreEqual("", s);
                    Assert.AreEqual(true, b);
                    Assert.AreEqual(1, i);
            
                    Assert.AreSame("", s);
                    Assert.AreSame(true, b);
                    Assert.AreSame(1, i);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task LiteralUsingNamedArgument()
    {
        var code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;

                    [|Assert.AreEqual(actual: "", expected: s)|];
                    [|Assert.AreEqual(actual: true, expected: b)|];
                    [|Assert.AreEqual(actual: 1, expected: i)|];

                    [|Assert.AreSame(actual: "", expected: s)|];
                    [|Assert.AreSame(actual: true, expected: b)|];
                    [|Assert.AreSame(actual: 1, expected: i)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    bool b = true;
                    int i = 42;
            
                    Assert.AreEqual(actual: s, expected: "");
                    Assert.AreEqual(actual: b, expected: true);
                    Assert.AreEqual(actual: i, expected: 1);
            
                    Assert.AreSame(actual: s, expected: "");
                    Assert.AreSame(actual: b, expected: true);
                    Assert.AreSame(actual: i, expected: 1);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
