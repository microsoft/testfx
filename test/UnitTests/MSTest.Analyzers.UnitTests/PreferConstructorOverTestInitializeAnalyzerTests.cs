// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.PreferConstructorOverTestInitializeAnalyzer,
    MSTest.Analyzers.PreferConstructorOverTestInitializeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class PreferConstructorOverTestInitializeAnalyzerTests
{
    [TestMethod]
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

    [TestMethod]
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
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public MyTestClass()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
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
        string fixedCode = """
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

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestInitializeAndCtorWithBody_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x, y;
                public MyTestClass()
                {
                    y=1;
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                    x=1;
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x, y;
                public MyTestClass()
                {
                    y=1;
                    x=1;
                }
            }
            """
        ;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestInitializeAndCtorWithBothHavingBody_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x, y;
                public MyTestClass()
                {
                    y=1;
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                    if(y == 1)
                    {
                        x = 2;
                    }
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int x, y;
                public MyTestClass()
                {
                    y=1;
                    if(y == 1)
                    {
                        x = 2;
                    }
                }
            }
            """
        ;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasTestInitializeAndTwoCtor_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int _x, _y;
                public MyTestClass()
                {
                    _y=1;
                }

                public MyTestClass(int y)
                {
                    _y=y;
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                    _x=1;
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                int _x, _y;
                public MyTestClass()
                {
                    _y=1;
                    _x=1;
                }

                public MyTestClass(int y)
                {
                    _y=y;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasStaticCtorAndTestInitialize_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private object _instanceVariable;

                static MyTestClass()
                {
                }

                [TestInitialize]
                public void [|MyTestInit|]()
                {
                    _instanceVariable = new object();
                }
            }
            """;
        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private object _instanceVariable;

                static MyTestClass()
                {
                }

                public MyTestClass()
                {
                    _instanceVariable = new object();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
