// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodShouldBeValidAnalyzer,
    MSTest.Analyzers.TestMethodShouldBeValidCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestGroup]
public sealed class TestMethodShouldBeValidAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
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

    [Arguments("protected")]
    [Arguments("internal")]
    [Arguments("internal protected")]
    [Arguments("private")]
    public async Task WhenTestMethodIsNotPublic_Diagnostic(string accessibility)
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                {{accessibility}} void [|MyTestMethod|]()
                {
                }
            }
            """;

        string codeFix = $$"""
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

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

    public async Task WhenMethodIsNotPublicAndNotTestMethod_NoDiagnostic()
    {
        string code = $$"""
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

        string codeFix = """
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

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

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

        string codeFix = """
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

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

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
            }
            """;

        string codeFix = """
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

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

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

        string codeFix = """
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

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

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
                    await Task.Delay(0);
                }
            }
            """;

        string codeFix = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    await Task.Delay(0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }

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
            
            public class Outer
            {
                [TestClass]
                private class MyTestClass2
                {
                    [TestMethod]
                    public void [|MyTestMethod|]()
                    {
                    }
                }

                [TestClass]
                private class MyTestClass3
                {
                    [TestMethod]
                    private void [|MyTestMethod|]()
                    {
                    }
                }
            }
            """;

        string codeFix = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Threading.Tasks;

            [assembly: DiscoverInternals]

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                private void MyTestMethod()
                {
                }
            }
            
            public class Outer
            {
                [TestClass]
                private class MyTestClass2
                {
                    [TestMethod]
                    public void MyTestMethod()
                    {
                    }
                }

                [TestClass]
                private class MyTestClass3
                {
                    [TestMethod]
                    private void MyTestMethod()
                    {
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, codeFix);
    }
}
