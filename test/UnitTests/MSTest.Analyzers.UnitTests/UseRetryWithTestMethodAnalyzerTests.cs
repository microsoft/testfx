// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseRetryWithTestMethodAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseRetryWithTestMethodAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasRetryAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Retry(maxRetryAttempts: 3)]
                public void M()
                {
                }

                [Retry(maxRetryAttempts: 3)]
                [TestMethod]
                public void M2()
                {
                }

                [MyTestMethod]
                [Retry(maxRetryAttempts: 3)]
                public void M3()
                {
                }
            
                [Retry(maxRetryAttempts: 3)]
                [MyTestMethod]
                public void M4()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodDoesNotHaveRetryAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void M()
                {
                }

                [MyTestMethod]
                public void M2()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasRetryAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Retry(maxRetryAttempts: 3)]
                public void [|M|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodDoesNotHaveRetryAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void M()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasRetryAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyTestClassAttribute : TestClassAttribute { }

            [TestClass]
            [Retry(maxRetryAttempts: 3)]
            public class MyTestClass
            {
                [TestMethod]
                public void M()
                {
                }
            }

            [Retry(maxRetryAttempts: 3)]
            [TestClass]
            public class MyTestClass2
            {
                [TestMethod]
                public void M()
                {
                }
            }

            [MyTestClass]
            [Retry(maxRetryAttempts: 3)]
            public class MyTestClass3
            {
                [TestMethod]
                public void M()
                {
                }
            }

            [Retry(maxRetryAttempts: 3)]
            [MyTestClass]
            public class MyTestClass4
            {
                [TestMethod]
                public void M()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasRetryAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [Retry(maxRetryAttempts: 3)]
            public class [|MyClass|]
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClassDoesNotHaveRetryAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClass
            {
                public void M()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodHasRetryAttribute_NoDiagnostic()
    {
        // DataTestMethodAttribute derives from TestMethodAttribute, so [Retry] on a [DataTestMethod] is valid.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [Retry(maxRetryAttempts: 3)]
                [DataRow(1)]
                public void M(int x)
                {
                }

                [Retry(maxRetryAttempts: 3)]
                [DataTestMethod]
                [DataRow(2)]
                public void M2(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestInitializeHasRetryAttribute_Diagnostic()
    {
        // [TestInitialize] is a fixture method, not a test method. Placing [Retry] on it is invalid.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestInitialize]
                [Retry(maxRetryAttempts: 3)]
                public void [|Setup|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMethodInNonTestClassHasRetryAttribute_Diagnostic()
    {
        // A method with [Retry] inside a class that is not decorated with [TestClass] should be flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyHelper
            {
                [Retry(maxRetryAttempts: 3)]
                public void [|M|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasCustomDerivedRetryAttribute_Diagnostic()
    {
        // The analyzer uses Inherits() to detect RetryBaseAttribute subclasses.
        // A user-defined subclass of RetryBaseAttribute on a non-test method should trigger the diagnostic.
        string code = """
            #pragma warning disable MSTESTEXP
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyRetryAttribute : RetryBaseAttribute
            {
                protected override Task<RetryResult> ExecuteAsync(RetryContext context) => Task.FromResult(new RetryResult());
            }
            #pragma warning restore MSTESTEXP

            [TestClass]
            public class MyTestClass
            {
                [MyRetry]
                public void [|M|]()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasCustomDerivedRetryAttribute_NoDiagnostic()
    {
        // A user-defined RetryBaseAttribute subclass on an actual test method should NOT trigger the diagnostic.
        string code = """
            #pragma warning disable MSTESTEXP
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyRetryAttribute : RetryBaseAttribute
            {
                protected override Task<RetryResult> ExecuteAsync(RetryContext context) => Task.FromResult(new RetryResult());
            }
            #pragma warning restore MSTESTEXP

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [MyRetry]
                public void M()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasCustomDerivedRetryAttribute_Diagnostic()
    {
        // A user-defined RetryBaseAttribute subclass on a non-test class should trigger the diagnostic.
        string code = """
            #pragma warning disable MSTESTEXP
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyRetryAttribute : RetryBaseAttribute
            {
                protected override Task<RetryResult> ExecuteAsync(RetryContext context) => Task.FromResult(new RetryResult());
            }
            #pragma warning restore MSTESTEXP

            [MyRetry]
            public class [|MyClass|]
            {
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestClassHasCustomDerivedRetryAttribute_NoDiagnostic()
    {
        // A user-defined RetryBaseAttribute subclass on an actual test class should NOT trigger the diagnostic.
        string code = """
            #pragma warning disable MSTESTEXP
            using System.Threading.Tasks;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyRetryAttribute : RetryBaseAttribute
            {
                protected override Task<RetryResult> ExecuteAsync(RetryContext context) => Task.FromResult(new RetryResult());
            }
            #pragma warning restore MSTESTEXP

            [TestClass]
            [MyRetry]
            public class MyTestClass
            {
                [TestMethod]
                public void M()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
