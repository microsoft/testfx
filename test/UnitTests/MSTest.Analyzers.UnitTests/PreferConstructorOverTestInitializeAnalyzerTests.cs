// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferConstructorOverTestInitializeAnalyzer,
    MSTest.Analyzers.PreferConstructorOverTestInitializeFixer>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class PreferConstructorOverTestInitializeAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenTestClassHasCtor_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenTestClassHasTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public void [|MyTestInit|]()
                {
                }
            }
            """;
        string fixeCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixeCode);
    }

    public async Task WhenTestClassHasTestInitializeAsync_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                public Task MyTestInit()
                {
                    return Task.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    public async Task WhenTestClassHasTestInitializeAndCtor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                }
            }
            """;
        string fixeCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """
        ;

        await VerifyCS.VerifyCodeFixAsync(code, fixeCode);
    }

    public async Task WhenTestClassHasTestInitializeAndCtorWithBody_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x;
                public MyTestClass()
                {
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                    x=1;
                }
            }
            """;
        string fixeCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x;
                public MyTestClass()
                {
                    x=1;
                }
            }
            """
        ;

        await VerifyCS.VerifyCodeFixAsync(code, fixeCode);
    }
}
