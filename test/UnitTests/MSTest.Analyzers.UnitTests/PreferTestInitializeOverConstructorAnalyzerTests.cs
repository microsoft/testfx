// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferTestInitializeOverConstructorAnalyzer,
    MSTest.Analyzers.PreferTestInitializeOverConstructorFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferTestInitializeOverConstructorAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestClassHasCtor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public [|MyTestClass|]()
                {
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void TestInitialize()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHas_TwoCtorandExsitesTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int y, x;

                [TestInitialize]
                public void BeforeEachTest()
                {
                    x=1;
                }

                public [|MyTestClass|]()
                {
                    if(y == 1)
                    {
                        x = 2;
                    }
                }

                public MyTestClass(int i)
                {
                    x=y;
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int y, x;

                [TestInitialize]
                public void BeforeEachTest()
                {
                    x=1;
                    if(y == 1)
                    {
                        x = 2;
                    }
                }

                public MyTestClass(int i)
                {
                    x=y;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClass_WithLocalTestInitializeAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public class TestInitializeAttribute : Attribute { }

            [TestClass]
            public class MyTestClass
            {
                int x;

                [TestInitialize]
                public void BeforeEachTest()
                {
                }

                public [|MyTestClass|]()
                {
                    x=1;
                }
            }
            """;
        // It adds a TestInitialize but it will use the local TestInitialize and this is wrong behavior
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System;

            [AttributeUsage(AttributeTargets.Method)]
            public class TestInitializeAttribute : Attribute { }

            [TestClass]
            public class MyTestClass
            {
                int x;

                [TestInitialize]
                public void BeforeEachTest()
                {
                }

                [TestInitialize]
                public void TestInitialize()
                {
                    x=1;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasImplicitCtor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasParameterizedCtor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass(int i)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasCtorWithMultiLineBody_PreservesIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int _x;

                public [|MyTestClass|]()
                {
                    _x = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private int _x;

                [TestInitialize]
                public void TestInitialize()
                {
                    _x = SomeMethod(
                        1,
                        2,
                        3);
                }

                private int SomeMethod(int a, int b, int c) => a + b + c;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
