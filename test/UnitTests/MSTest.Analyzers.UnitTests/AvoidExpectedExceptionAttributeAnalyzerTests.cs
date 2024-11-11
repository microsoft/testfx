// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidExpectedExceptionAttributeAnalyzer,
    MSTest.Analyzers.AvoidExpectedExceptionAttributeFixer>;

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

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                }

                [TestMethod]
                public void TestMethod2()
                {
                }

                [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
                [TestMethod]
                public void TestMethod3()
                {
                }

                [ExpectedException(typeof(System.Exception), "Some message", AllowDerivedTypes = true)]
                [TestMethod]
                public void TestMethod4()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestState =
            {
                Sources =
                {
                    code,
                },
            },
            FixedState =
            {
                Sources =
                {
                    fixedCode,
                },
                ExpectedDiagnostics =
                {
                    // /0/Test0.cs(18,17): info MSTEST0006: Prefer 'Assert.ThrowsException/ThrowsExceptionAsync' over '[ExpectedException]'
                    VerifyCS.Diagnostic().WithSpan(18, 17, 18, 28),
                    // /0/Test0.cs(24,17): info MSTEST0006: Prefer 'Assert.ThrowsException/ThrowsExceptionAsync' over '[ExpectedException]'
                    VerifyCS.Diagnostic().WithSpan(24, 17, 24, 28),
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    public async Task When_Statement_Block()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public void [|TestMethod|]()
                {
                    Console.WriteLine("Hello, world!");
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.ThrowsException<Exception>(() => Console.WriteLine("Hello, world!"));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task When_Statement_ExpressionBody()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public void [|TestMethod|]()
                    => Console.WriteLine("Hello, world!");
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                    => Assert.ThrowsException<Exception>(() => Console.WriteLine("Hello, world!"));
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task When_Expression_Block()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                private int GetNumber() => 0;

                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public void [|TestMethod|]()
                {
                    GetNumber();
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                private int GetNumber() => 0;

                [TestMethod]
                public void TestMethod()
                {
                    Assert.ThrowsException<Exception>(() => GetNumber());
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task When_Expression_ExpressionBody()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                private int GetNumber() => 0;

                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public void [|TestMethod|]()
                    => GetNumber();
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                private int GetNumber() => 0;
                [TestMethod]
                public void TestMethod()
                    => Assert.ThrowsException<Exception>(() => GetNumber());
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task When_Async_Block()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public async Task [|TestMethod|]()
                {
                    await Task.Delay(0);
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    await Assert.ThrowsExceptionAsync<Exception>(async () => await Task.Delay(0));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    public async Task When_Async_ExpressionBody()
    {
        string code = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [ExpectedException(typeof(System.Exception))]
                [TestMethod]
                public async Task [|TestMethod|]()
                    => await Task.Delay(0);
            }
            """;

        string fixedCode = """
            using System;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public async Task TestMethod()
                    => await Assert.ThrowsExceptionAsync<Exception>(async () => await Task.Delay(0));
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
