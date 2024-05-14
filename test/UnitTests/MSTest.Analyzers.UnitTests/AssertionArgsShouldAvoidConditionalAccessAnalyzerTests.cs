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
    public async Task WhenUsingConditionalsAccess_In_Assert_Equal()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    A? a = new A();
                    A b = new A();
                    string? s = "";
            
                    [|Assert.AreEqual(s?.Length, 32)|];
                    [|Assert.AreEqual(((s?.Length)), 32)|];
                    [|Assert.AreEqual(s?.Length, s?.Length)|];
                    [|Assert.AreEqual(a?.S?.Length, 32)|];
                    [|Assert.AreEqual(b.S?.Length, 32)|];

                    [|Assert.AreNotEqual(s?.Length, 32)|];
                    [|Assert.AreNotEqual(((s?.Length)), 32)|];
                    [|Assert.AreNotEqual(s?.Length, s?.Length)|];
                    [|Assert.AreNotEqual(a?.S?.Length, 32)|];
                    [|Assert.AreNotEqual(b.S?.Length, 32)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string? s = "";
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(s);
                    Assert.AreEqual(s.Length, 32);
                    Assert.AreEqual(((s.Length)), 32);
                    Assert.AreEqual(s.Length, s.Length);
                    
                    Assert.IsNotNull(a);
                    Assert.IsNotNull(a.S);
                    Assert.IsNotNull(b);
                    Assert.IsNotNull(b.S);
                    Assert.AreEqual(a.S.Length, 32);
                    Assert.AreEqual(b.S.Length, 32);
                    Assert.AreNotEqual(s.Length, 32);
                    Assert.AreNotEqual(((s.Length)), 32);
                    Assert.AreNotEqual(s.Length, s.Length);
                    Assert.AreNotEqual(a.S.Length, 32);
                    Assert.AreNotEqual(b.S.Length, 32);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenUsingConditionalsAccess_In_Assert_Boolean()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    A? a = new A();
                    A b = new A();
                    string? s = "";
            
                    [|Assert.IsTrue(s?.Length > 32)|];
                    [|Assert.IsTrue((s?.Length> 32))|];
                    [|Assert.IsTrue(a?.S?.Length > 32)|];
                    [|Assert.IsTrue(b.S?.Length > 32)|];
                    [|Assert.IsFalse(s?.Length > 32)|];
                    [|Assert.IsFalse((s?.Length > 32))|];
                    [|Assert.IsFalse(a?.S?.Length > 32)|];
                    [|Assert.IsFalse(b.S?.Length > 32)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string? s = "";
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(s);
                    Assert.IsNotNull(a);
                    Assert.IsNotNull(a.S);
                    Assert.IsNotNull(b);
                    Assert.IsNotNull(b.S);
                    Assert.IsTrue(s.Length > 32);
                    Assert.IsTrue((s.Length > 32));
                    Assert.IsTrue(a.S.Length > 32);
                    Assert.IsTrue(b.S.Length > 32);
                    Assert.IsFalse(s.Length > 32);
                    Assert.IsFalse((s.Length > 32));
                    Assert.IsFalse(a.S.Length > 32);
                    Assert.IsFalse(b.S.Length > 32);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenUsingConditionalsAccess_In_CollectionAssert_Equal()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public List<string>? S { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    A? a = new A();
                    A b = new A();
            
                    [|CollectionAssert.AreEqual(a?.S, b.S)|];
                    [|CollectionAssert.AreEqual((a?.S), b.S)|];
                    [|CollectionAssert.AreEqual(b.S, a?.S)|];
                    [|CollectionAssert.AreEqual(a?.S, a?.S)|];
                    [|CollectionAssert.AreNotEqual(a?.S, b.S)|];
                    [|CollectionAssert.AreNotEqual((a?.S), b.S)|];
                    [|CollectionAssert.AreNotEqual(b.S, a?.S)|];
                    [|CollectionAssert.AreNotEqual(a?.S, a?.S)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(a);
                    CollectionAssert.AreEqual(a.S, b.S);
                    CollectionAssert.AreEqual((a.S), b.S);
                    CollectionAssert.AreEqual(b.S, a.S);
                    CollectionAssert.AreEqual(a.S, a.S);
                    CollectionAssert.AreNotEqual(a.S, b.S);
                    CollectionAssert.AreNotEqual((a.S), b.S);
                    CollectionAssert.AreNotEqual(b.S, a.S);
                    CollectionAssert.AreNotEqual(a.S, a.S);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
