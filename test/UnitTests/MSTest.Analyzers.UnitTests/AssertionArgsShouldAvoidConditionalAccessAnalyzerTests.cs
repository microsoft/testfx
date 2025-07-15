// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AssertionArgsShouldAvoidConditionalAccessAnalyzer,
    MSTest.Analyzers.AssertionArgsShouldAvoidConditionalAccessFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AssertionArgsShouldAvoidConditionalAccessAnalyzerTests
{
    [TestMethod]
    public async Task WhenUsingConditionalsAccess_In_Assert_Equal()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
                public List<string>? T { get; set; }
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
                    [|Assert.AreEqual(a?.T?[3]?.Length, 32)|];
                    [|Assert.AreEqual(b.T?[3]?.Length, 32)|];

                    [|Assert.AreNotEqual(s?.Length, 32)|];
                    [|Assert.AreNotEqual(((s?.Length)), 32)|];
                    [|Assert.AreNotEqual(s?.Length, s?.Length)|];
                    [|Assert.AreNotEqual(a?.S?.Length, 32)|];
                    [|Assert.AreNotEqual(b.S?.Length, 32)|];
                    [|Assert.AreNotEqual(a?.T?[3]?.Length, 32)|];
                    [|Assert.AreNotEqual(b.T?[3]?.Length, 32)|];

                    [|Assert.AreSame(s?.Length, 32)|];
                    [|Assert.AreSame(((s?.Length)), 32)|];
                    [|Assert.AreSame(s?.Length, s?.Length)|];
                    [|Assert.AreSame(a?.S?.Length, 32)|];
                    [|Assert.AreSame(b.S?.Length, 32)|];
                    [|Assert.AreSame(a?.T?[3]?.Length, 32)|];
                    [|Assert.AreSame(b.T?[3]?.Length, 32)|];

                    [|Assert.AreNotSame(s?.Length, 32)|];
                    [|Assert.AreNotSame(((s?.Length)), 32)|];
                    [|Assert.AreNotSame(s?.Length, s?.Length)|];
                    [|Assert.AreNotSame(a?.S?.Length, 32)|];
                    [|Assert.AreNotSame(b.S?.Length, 32)|];
                    [|Assert.AreNotSame(a?.T?[3]?.Length, 32)|];
                    [|Assert.AreNotSame(b.T?[3]?.Length, 32)|];
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
                    Assert.IsNotNull(a.T);
                    Assert.IsNotNull(b);
                    Assert.IsNotNull(b.S);
                    Assert.IsNotNull(b.T);
                    Assert.AreEqual(s.Length, 32);
                    Assert.AreEqual(((s.Length)), 32);
                    Assert.AreEqual(s.Length, s.Length);
                    Assert.AreEqual(a.S.Length, 32);
                    Assert.AreEqual(b.S.Length, 32);
                    Assert.AreEqual(a.T[3].Length, 32);
                    Assert.AreEqual(b.T[3].Length, 32);
                    Assert.AreNotEqual(s.Length, 32);
                    Assert.AreNotEqual(((s.Length)), 32);
                    Assert.AreNotEqual(s.Length, s.Length);
                    Assert.AreNotEqual(a.S.Length, 32);
                    Assert.AreNotEqual(b.S.Length, 32);
                }
            }
            """;

        string fixedCode = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
                public List<string>? T { get; set; }
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
                    Assert.IsNotNull(s);
                    Assert.AreEqual(s.Length, 32);
                    Assert.AreEqual(((s.Length)), 32);
                    Assert.AreEqual(s.Length, s.Length);
                    Assert.IsNotNull(a);
                    Assert.IsNotNull(a.S);
                    Assert.AreEqual(a.S.Length, 32);
                    Assert.IsNotNull(b.S);
                    Assert.AreEqual(b.S.Length, 32);
                    Assert.IsNotNull(a.T);
                    Assert.IsNotNull(a.T[3]);
                    Assert.AreEqual(a.T[3].Length, 32);
                    Assert.IsNotNull(b.T);
                    Assert.IsNotNull(b.T[3]);
                    Assert.AreEqual(b.T[3].Length, 32);

                    Assert.AreNotEqual(s.Length, 32);
                    Assert.AreNotEqual(((s.Length)), 32);
                    Assert.AreNotEqual(s.Length, s.Length);
                    Assert.AreNotEqual(a.S.Length, 32);
                    Assert.AreNotEqual(b.S.Length, 32);
                    Assert.AreNotEqual(a.T[3].Length, 32);
                    Assert.AreNotEqual(b.T[3].Length, 32);

                    Assert.AreSame(s.Length, 32);
                    Assert.AreSame(((s.Length)), 32);
                    Assert.AreSame(s.Length, s.Length);
                    Assert.AreSame(a.S.Length, 32);
                    Assert.AreSame(b.S.Length, 32);
                    Assert.AreSame(a.T[3].Length, 32);
                    Assert.AreSame(b.T[3].Length, 32);

                    Assert.AreNotSame(s.Length, 32);
                    Assert.AreNotSame(((s.Length)), 32);
                    Assert.AreNotSame(s.Length, s.Length);
                    Assert.AreNotSame(a.S.Length, 32);
                    Assert.AreNotSame(b.S.Length, 32);
                    Assert.AreNotSame(a.T[3].Length, 32);
                    Assert.AreNotSame(b.T[3].Length, 32);
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
                    Assert.IsNotNull(a.T);
                    Assert.IsNotNull(b);
                    Assert.IsNotNull(b.S);
                    Assert.IsNotNull(b.T);
                    Assert.AreEqual(s.Length, 32);
                    Assert.AreEqual(((s.Length)), 32);
                    Assert.AreEqual(s.Length, s.Length);
                    Assert.AreEqual(a.S.Length, 32);
                    Assert.AreEqual(b.S.Length, 32);
                    Assert.AreEqual(a.T[3].Length, 32);
                    Assert.AreEqual(b.T[3].Length, 32);
                    Assert.AreNotEqual(s.Length, 32);
                    Assert.AreNotEqual(((s.Length)), 32);
                    Assert.AreNotEqual(s.Length, s.Length);
                    Assert.AreNotEqual(a.S.Length, 32);
                    Assert.AreNotEqual(b.S.Length, 32);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 3,
            NumberOfFixAllInDocumentIterations = 3,
            NumberOfFixAllInProjectIterations = 3,
            NumberOfIncrementalIterations = 48,
        }.RunAsync();
    }

    [TestMethod]
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

        string fixedCode = """
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
                    Assert.IsNotNull(s);
                    Assert.IsTrue(s.Length > 32);
                    Assert.IsTrue((s.Length> 32));
                    Assert.IsNotNull(a);
                    Assert.IsNotNull(a.S);
                    Assert.IsTrue(a.S.Length > 32);
                    Assert.IsNotNull(b.S);
                    Assert.IsTrue(b.S.Length > 32);
                    Assert.IsFalse(s.Length > 32);
                    Assert.IsFalse((s.Length > 32));
                    Assert.IsFalse(a.S.Length > 32);
                    Assert.IsFalse(b.S.Length > 32);
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

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 2,
            NumberOfFixAllInDocumentIterations = 2,
            NumberOfFixAllInProjectIterations = 2,
            NumberOfIncrementalIterations = 10,
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenUsingConditionalsAccess_In_NullAssertions_Gives_NoWarning()
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
                public void Compliant()
                {
                    A? a = new A();

                    Assert.IsNull(a?.S);
                    Assert.IsNotNull(a?.S);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenUsingConditionalsAccess_In_CollectionAssert()
    {
        string code = """
            #nullable enable
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public List<string>? S { get; set; }
                public Type? T { get; set; }
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
                    [|CollectionAssert.AreEqual(a?.S, a?.S)|];
                    [|CollectionAssert.AreEqual(b.S, a?.S)|];
                    [|CollectionAssert.AreNotEqual(a?.S, b.S)|];
                    [|CollectionAssert.AreNotEqual(a?.S, a?.S)|];
                    [|CollectionAssert.AreNotEqual(b.S, a?.S)|];
                    [|CollectionAssert.Contains(a?.S, a)|];
                    [|CollectionAssert.Contains(b.S, a?.S)|];
                    [|CollectionAssert.Contains(a?.S, a?.S)|];
                    [|CollectionAssert.DoesNotContain(a?.S, a)|];
                    [|CollectionAssert.DoesNotContain(b.S, a?.S)|];
                    [|CollectionAssert.DoesNotContain(a?.S, a?.S)|];
                    [|CollectionAssert.AllItemsAreNotNull(a?.S)|];
                    [|CollectionAssert.AllItemsAreUnique(a?.S)|];
                    [|CollectionAssert.IsSubsetOf(a?.S, b.S)|];
                    [|CollectionAssert.IsSubsetOf(a?.S, a?.S)|];
                    [|CollectionAssert.IsSubsetOf(b.S, a?.S)|];
                    [|CollectionAssert.IsNotSubsetOf(a?.S, b.S)|];
                    [|CollectionAssert.IsNotSubsetOf(a?.S, a?.S)|];
                    [|CollectionAssert.IsNotSubsetOf(b.S, a?.S)|];
                    [|CollectionAssert.AllItemsAreInstancesOfType(a?.S, b.T)|];
                    [|CollectionAssert.AllItemsAreInstancesOfType(a?.S, a?.T)|];
                    [|CollectionAssert.AllItemsAreInstancesOfType(b.S, a?.T)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(a);
                    CollectionAssert.AreEqual(a.S, b.S);
                    CollectionAssert.AreNotEqual(a.S, b.S);
                    CollectionAssert.Contains(a.S, a);
                    CollectionAssert.DoesNotContain(a.S, a);
                    CollectionAssert.AllItemsAreNotNull(a.S);
                    CollectionAssert.AllItemsAreUnique(a.S);
                    CollectionAssert.IsSubsetOf(a.S, b.S);
                    CollectionAssert.IsNotSubsetOf(a.S, b.S);
                    CollectionAssert.AllItemsAreInstancesOfType(a.S, a.T);
                }
            }
            """;

        string fixedCode = """
            #nullable enable
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public List<string>? S { get; set; }
                public Type? T { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    A? a = new A();
                    A b = new A();
                    Assert.IsNotNull(a);
                    CollectionAssert.AreEqual(a.S, b.S);
                    CollectionAssert.AreEqual(a.S, a.S);
                    CollectionAssert.AreEqual(b.S, a.S);
                    CollectionAssert.AreNotEqual(a.S, b.S);
                    CollectionAssert.AreNotEqual(a.S, a.S);
                    CollectionAssert.AreNotEqual(b.S, a.S);
                    CollectionAssert.Contains(a.S, a);
                    CollectionAssert.Contains(b.S, a.S);
                    CollectionAssert.Contains(a.S, a.S);
                    CollectionAssert.DoesNotContain(a.S, a);
                    CollectionAssert.DoesNotContain(b.S, a.S);
                    CollectionAssert.DoesNotContain(a.S, a.S);
                    CollectionAssert.AllItemsAreNotNull(a.S);
                    CollectionAssert.AllItemsAreUnique(a.S);
                    CollectionAssert.IsSubsetOf(a.S, b.S);
                    CollectionAssert.IsSubsetOf(a.S, a.S);
                    CollectionAssert.IsSubsetOf(b.S, a.S);
                    CollectionAssert.IsNotSubsetOf(a.S, b.S);
                    CollectionAssert.IsNotSubsetOf(a.S, a.S);
                    CollectionAssert.IsNotSubsetOf(b.S, a.S);
                    CollectionAssert.AllItemsAreInstancesOfType(a.S, b.T);
                    CollectionAssert.AllItemsAreInstancesOfType(a.S, a.T);
                    CollectionAssert.AllItemsAreInstancesOfType(b.S, a.T);
                }

                [TestMethod]
                public void Compliant()
                {
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(a);
                    CollectionAssert.AreEqual(a.S, b.S);
                    CollectionAssert.AreNotEqual(a.S, b.S);
                    CollectionAssert.Contains(a.S, a);
                    CollectionAssert.DoesNotContain(a.S, a);
                    CollectionAssert.AllItemsAreNotNull(a.S);
                    CollectionAssert.AllItemsAreUnique(a.S);
                    CollectionAssert.IsSubsetOf(a.S, b.S);
                    CollectionAssert.IsNotSubsetOf(a.S, b.S);
                    CollectionAssert.AllItemsAreInstancesOfType(a.S, a.T);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 2,
            NumberOfFixAllInDocumentIterations = 2,
            NumberOfFixAllInProjectIterations = 2,
            NumberOfIncrementalIterations = 30,
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenUsingConditionalsAccess_In_StringAssert()
    {
        string code = """
            #nullable enable
            using System.Text.RegularExpressions;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
                public Regex? R { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    Regex r = new Regex("");
                    A? a = new A();
                    A b = new A();

                    [|StringAssert.Contains(a?.S, s)|];
                    [|StringAssert.Contains(a?.S, a?.S)|];
                    [|StringAssert.Contains(b.S, a?.S)|];
                    [|StringAssert.StartsWith(a?.S, s)|];
                    [|StringAssert.StartsWith(a?.S, a?.S)|];
                    [|StringAssert.StartsWith(s, a?.S)|];
                    [|StringAssert.EndsWith(a?.S, s)|];
                    [|StringAssert.EndsWith(a?.S, a?.S)|];
                    [|StringAssert.EndsWith(s, a?.S)|];
                    [|StringAssert.Matches(a?.S, r)|];
                    [|StringAssert.Matches(a?.S, a?.R)|];
                    [|StringAssert.Matches(s, a?.R)|];
                    [|StringAssert.DoesNotMatch(a?.S, r)|];
                    [|StringAssert.DoesNotMatch(a?.S, a?.R)|];
                    [|StringAssert.DoesNotMatch(s, a?.R)|];
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    Regex r = new Regex("");
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(a);
                    StringAssert.Contains(a.S, s);
                    StringAssert.Contains(a.S, a.S);
                    StringAssert.Contains(b.S, a.S);
                    StringAssert.StartsWith(a.S, s);
                    StringAssert.StartsWith(a.S, a.S);
                    StringAssert.StartsWith(s, a.S);
                    StringAssert.EndsWith(a.S, s);
                    StringAssert.EndsWith(a.S, a.S);
                    StringAssert.EndsWith(s, a.S);
                    StringAssert.Matches(a.S, r);
                    StringAssert.Matches(a.S, a.R);
                    StringAssert.Matches(s, a.R);
                    StringAssert.DoesNotMatch(a.S, r);
                    StringAssert.DoesNotMatch(a.S, a.R);
                    StringAssert.DoesNotMatch(s, a.R);
                }
            }
            """;

        string fixedCode = """
            #nullable enable
            using System.Text.RegularExpressions;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
                public Regex? R { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void NonCompliant()
                {
                    string s = "";
                    Regex r = new Regex("");
                    A? a = new A();
                    A b = new A();
                    Assert.IsNotNull(a);
                    StringAssert.Contains(a.S, s);
                    StringAssert.Contains(a.S, a.S);
                    StringAssert.Contains(b.S, a.S);
                    StringAssert.StartsWith(a.S, s);
                    StringAssert.StartsWith(a.S, a.S);
                    StringAssert.StartsWith(s, a.S);
                    StringAssert.EndsWith(a.S, s);
                    StringAssert.EndsWith(a.S, a.S);
                    StringAssert.EndsWith(s, a.S);
                    StringAssert.Matches(a.S, r);
                    StringAssert.Matches(a.S, a.R);
                    StringAssert.Matches(s, a.R);
                    StringAssert.DoesNotMatch(a.S, r);
                    StringAssert.DoesNotMatch(a.S, a.R);
                    StringAssert.DoesNotMatch(s, a.R);
                }

                [TestMethod]
                public void Compliant()
                {
                    string s = "";
                    Regex r = new Regex("");
                    A? a = new A();
                    A b = new A();

                    Assert.IsNotNull(a);
                    StringAssert.Contains(a.S, s);
                    StringAssert.Contains(a.S, a.S);
                    StringAssert.Contains(b.S, a.S);
                    StringAssert.StartsWith(a.S, s);
                    StringAssert.StartsWith(a.S, a.S);
                    StringAssert.StartsWith(s, a.S);
                    StringAssert.EndsWith(a.S, s);
                    StringAssert.EndsWith(a.S, a.S);
                    StringAssert.EndsWith(s, a.S);
                    StringAssert.Matches(a.S, r);
                    StringAssert.Matches(a.S, a.R);
                    StringAssert.Matches(s, a.R);
                    StringAssert.DoesNotMatch(a.S, r);
                    StringAssert.DoesNotMatch(a.S, a.R);
                    StringAssert.DoesNotMatch(s, a.R);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 2,
            NumberOfFixAllInDocumentIterations = 2,
            NumberOfFixAllInProjectIterations = 2,
            NumberOfIncrementalIterations = 20,
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenExpressionBody()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class MyClass
            {
                public MyClass A { get; set; }
                public MyClass B { get; set; }
                public MyClass C { get; set; }
                public MyClass D { get; set; }
                public MyClass E { get; set; }
                public MyClass F { get; set; }

                public bool MyBool { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void Case1() // NOTE: No easy way for us to fix this properly
                    => [|Assert.IsTrue(new MyClass().A?.B.C?.D.E.MyBool)|];

                [TestMethod]
                public void Case2() // NOTE: No easy way for us to fix this properly
                    => [|Assert.IsTrue(new MyClass().A?.B.C.D.E.MyBool)|];
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;
            
            public class MyClass
            {
                public MyClass A { get; set; }
                public MyClass B { get; set; }
                public MyClass C { get; set; }
                public MyClass D { get; set; }
                public MyClass E { get; set; }
                public MyClass F { get; set; }

                public bool MyBool { get; set; }
            }
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void Case1() // NOTE: No easy way for us to fix this properly
                {
                    Assert.IsNotNull(new MyClass().A);
                    Assert.IsNotNull(new MyClass().A.B.C);
                    Assert.IsTrue(new MyClass().A.B.C.D.E.MyBool);
                }

                [TestMethod]
                public void Case2() // NOTE: No easy way for us to fix this properly
                {
                    Assert.IsNotNull(new MyClass().A);
                    Assert.IsTrue(new MyClass().A.B.C.D.E.MyBool);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 2,
            NumberOfFixAllInDocumentIterations = 2,
            NumberOfFixAllInProjectIterations = 2,
            NumberOfIncrementalIterations = 3,
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenUsingConditionalsAccess_In_Message_NoDiagnostic()
    {
        string code = """
            #nullable enable
            using System.Text.RegularExpressions;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            public class A
            {
                public string? S { get; set; }
                public Regex? R { get; set; }
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void Compliant()
                {
                    Assert.AreEqual(new object(), new object(), "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
