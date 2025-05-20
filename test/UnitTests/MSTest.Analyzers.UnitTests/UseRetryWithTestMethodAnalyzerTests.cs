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
}
