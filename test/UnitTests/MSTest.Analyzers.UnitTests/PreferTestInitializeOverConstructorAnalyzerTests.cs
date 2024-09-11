// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferTestInitializeOverConstructorAnalyzer,
    MSTest.Analyzers.PreferTestInitializeOverConstructorFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PreferTestInitializeOverConstructorAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
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

    public async Task WhenTestClassHas_TwoCtorandExsitesTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int y, x;
            
                [TestInitialize]
                public void TestInit()
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
                public void TestInit()
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
}
