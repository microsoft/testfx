// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertionArgsShouldAvoidConditionalAccessAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestGroup]
public sealed class AssertionArgsShouldAvoidConditionalAccessAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenUsingConditionalsAccess()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? B { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    A? a = new A();
                    A c = new A();
                    string? s = "";
            
                    [|Assert.AreEqual(s?.Length, 32)|];
                    [|Assert.AreEqual(s?.Length, s?.Length)|];
                    [|Assert.AreEqual(a?.B?.Length, 32)|];
                    [|Assert.AreEqual(c.B?.Length, 32)|];

                    [|Assert.AreNotEqual(s?.Length, 32)|];
                    [|Assert.AreNotEqual(s?.Length, s?.Length)|];
                    [|Assert.AreNotEqual(a?.B?.Length, 32)|];
                    [|Assert.AreNotEqual(c.B?.Length, 32)|];

                    [|Assert.IsTrue(s?.Length > 32);|]
                    [|Assert.IsTrue(s?.Length > a?.B?.Length);|]
                    [|Assert.IsFalse(s?.Length > 32);|]
                    [|Assert.IsFalse(s?.Length > a?.B?.Length);|]
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
