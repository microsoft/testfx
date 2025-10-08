// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldBeValidAnalyzer,
    MSTest.Analyzers.TestMethodShouldBeValidCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestMethodShouldBeValidAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodIsPublic_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [DataRow("protected")]
    [DataRow("internal")]
    [DataRow("internal protected")]
    [DataRow("private")]
    [TestMethod]
    public async Task WhenTestMethodIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                {{accessibility}} void [|MyTestMethod1|]()
                {
                }

                [TestMethod]
                {{accessibility}} async Task [|MyTestMethod2|]()
                {
                }
            }
            """;

        string fixedCode =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod1()
                {
                }

                [TestMethod]
                public async Task MyTestMethod2()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodIsNotPublicAndNotTestMethod_NoDiagnostic()
    {
        string code =
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private void PrivateMethod()
                {
                }

                protected void ProtectedMethod()
                {
                }

                internal protected void InternalProtectedMethod()
                {
                }

                internal void InternalMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsStatic_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public static void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodIsAbstract_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestMethod]
                public abstract void [|MyTestMethod|]();
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public abstract class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodIsGeneric_CanBeInferred_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod1<T>(T[] t)
                {
                }

                [TestMethod]
                public void TestMethod2<T>(List<T> t)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsGeneric_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void [|MyTestMethod|]<T>()
                {
                }

                [TestMethod]
                [DataRow(0)]
                public void MyTestMethod<T>(T t)
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }

                [TestMethod]
                [DataRow(0)]
                public void MyTestMethod<T>(T t)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodIsNotOrdinary_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                ~[|MyTestClass|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

#if NET
    [TestMethod]
    public async Task WhenTestMethodReturnTypeIsNotValid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public int [|MyTestMethod0|]()
                {
                    return 42;
                }

                [TestMethod]
                public string [|MyTestMethod1|]()
                {
                    return "42";
                }

                [TestMethod]
                public Task<int> [|MyTestMethod2|]()
                {
                    return Task.FromResult(42);
                }

                [TestMethod]
                public ValueTask<int> [|MyTestMethod3|]()
                {
                    return ValueTask.FromResult(42);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod0()
                {
                }

                [TestMethod]
                public void MyTestMethod1()
                {
                }

                [TestMethod]
                public void MyTestMethod2()
                {
                }

                [TestMethod]
                public void MyTestMethod3()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodReturnTypeIsValid_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod0()
                {
                }

                [TestMethod]
                public Task MyTestMethod1()
                {
                    return Task.CompletedTask;
                }

                [TestMethod]
                public ValueTask MyTestMethod2()
                {
                    return ValueTask.CompletedTask;
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
#endif

    [TestMethod]
    public async Task WhenTestMethodIsAsyncVoid_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public async Task MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodIsInternalAndDiscoverInternals_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass2
            {
                [TestMethod]
                internal void MyTestMethod()
                {
                }
            }

            [TestClass]
            internal class MyTestClass3
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodIsPrivateAndDiscoverInternals_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                private void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;
            
            [assembly: DiscoverInternals]
            
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
