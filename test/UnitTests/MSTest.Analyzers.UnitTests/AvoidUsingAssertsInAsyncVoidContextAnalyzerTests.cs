// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidUsingAssertsInAsyncVoidContextAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidUsingAssertsInAsyncVoidContextAnalyzerTests
{
    [TestMethod]
    public async Task UseAssertMethodInAsyncTaskMethod_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task TestMethod()
                {
                    Assert.Fail("");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncTaskDelegate_NoDiagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate Task MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        Assert.Fail("");
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|Assert.Fail("")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidDelegate_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate void MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        [|Assert.Fail("")|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseAssertMethodInAsyncVoidLocalFunction_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async void d()
                    {
                        await Task.Delay(1);
                        [|Assert.Fail("")|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseStringAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|StringAssert.Contains("abc", "a")|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseCollectionAssertMethodInAsyncVoidMethod_Diagnostic()
    {
        string code = """
            using System.Collections;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void TestMethod()
                {
                    await Task.Delay(1);
                    [|CollectionAssert.AreEqual(new[] { 1 }, new[] { 1 })|];
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseStringAssertMethodInAsyncVoidLocalFunction_Diagnostic()
    {
        string code = """
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    async void d()
                    {
                        await Task.Delay(1);
                        [|StringAssert.Contains("abc", "a")|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task UseCollectionAssertMethodInAsyncVoidDelegate_Diagnostic()
    {
        string code = """
            using System.Collections;
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                internal delegate void MyDelegate();

                [TestMethod]
                public void TestMethod()
                {
                    MyDelegate d = async () =>
                    {
                        await Task.Delay(1);
                        [|CollectionAssert.AreEqual(new[] { 1 }, new[] { 1 })|];
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
