﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidExpectedExceptionAttributeAnalyzer,
    MSTest.Analyzers.AvoidExpectedExceptionAttributeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidExpectedExceptionAttributeAnalyzerTests
{
    [TestMethod]
    public async Task WhenUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
            {
                protected override void Verify(System.Exception exception) { }
            }

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

                [MyExpectedException]
                [TestMethod]
                public void [|TestMethod5|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
            {
                protected override void Verify(System.Exception exception) { }
            }

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

                [TestMethod]
                public void TestMethod3()
                {
                }

                [TestMethod]
                public void TestMethod4()
                {
                }

                [MyExpectedException]
                [TestMethod]
                public void [|TestMethod5|]()
                {
                }
            }
            """;

        var test = new VerifyCS.Test
        {
            TestCode = code,
            FixedState =
            {
                Sources =
                {
                    fixedCode,
                },
                // The codefix cannot fix MyExpectedException because it cannot detect the exception type.
                // For now, the user needs to manually fix this.
                // We *could* provide a codefix that uses Assert.ThrowsException<SameExceptionType> but that's most likely going to be wrong.
                // NOTE: For fixed state, the default is MarkupMode.IgnoreFixable, so we set
                // to Allow as we still have expected errors after applying the codefix.
                MarkupHandling = MarkupMode.Allow,
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task When_Statement_Block_Diagnostic()
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

                [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
                [TestMethod]
                public void [|TestMethod2|]()
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
                    Assert.ThrowsExactly<Exception>(() => Console.WriteLine("Hello, world!"));
                }

                [TestMethod]
                public void TestMethod2()
                {
                    Assert.Throws<Exception>(() => Console.WriteLine("Hello, world!"));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_Statement_ExpressionBody_Diagnostic()
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
                    => Assert.ThrowsExactly<Exception>(() => Console.WriteLine("Hello, world!"));
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_Expression_Block_Diagnostic()
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
                    Assert.ThrowsExactly<Exception>(() => GetNumber());
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_Expression_ExpressionBody_Diagnostic()
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
                    => Assert.ThrowsExactly<Exception>(() => GetNumber());
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_Async_Block_Diagnostic()
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

                [ExpectedException(typeof(System.Exception), AllowDerivedTypes = true)]
                [TestMethod]
                public async Task [|TestMethod2|]()
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
                    await Assert.ThrowsExactlyAsync<Exception>(async () => await Task.Delay(0));
                }

                [TestMethod]
                public async Task TestMethod2()
                {
                    await Assert.ThrowsAsync<Exception>(async () => await Task.Delay(0));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_Async_ExpressionBody_Diagnostic()
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
                    => await Assert.ThrowsExactlyAsync<Exception>(async () => await Task.Delay(0));
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_TestMethodIsAsyncButLastStatementIsSynchronous_Diagnostic()
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
                    M();
                }

                private static void M() => throw new Exception();
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
                    await Task.Delay(0);
                    Assert.ThrowsExactly<Exception>(() => M());
                }

                private static void M() => throw new Exception();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task When_LastStatementHasDeepAwait_Diagnostic()
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
                    Console.WriteLine("Hello, world!");
                    // In ideal world, it's best if the codefix can separate await M() to a
                    // variable, then only wrap M(someVariable) in Assert.ThrowsException
                    // Let's also have this comment serve as a test for trivia ;)
                    M(await M());
                }

                private static Task<int> M() => Task.FromResult(0);

                private static void M(int _) => throw new Exception();
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
                    Console.WriteLine("Hello, world!");
                    await Assert.ThrowsExactlyAsync<Exception>(async () =>
                            // In ideal world, it's best if the codefix can separate await M() to a
                            // variable, then only wrap M(someVariable) in Assert.ThrowsException
                            // Let's also have this comment serve as a test for trivia ;)
                            M(await M()));
                }

                private static Task<int> M() => Task.FromResult(0);
            
                private static void M(int _) => throw new Exception();
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
