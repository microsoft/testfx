// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseCooperativeCancellationForTimeoutAnalyzer,
    MSTest.Analyzers.UseCooperativeCancellationForTimeoutFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class UseCooperativeCancellationForTimeoutAnalyzerTests
{
    [TestMethod]
    public async Task WhenTimeoutAttributeHasCooperativeCancellationTrue_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Timeout(5000, CooperativeCancellation = true)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithoutCooperativeCancellation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(5000)|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithCooperativeCancellationFalse_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(5000, CooperativeCancellation = false)|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeOnNonTestMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Timeout(5000)]
                public void NonTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodWithoutTimeoutAttribute_NoDiagnostic()
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

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithTestTimeoutEnum_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(TestTimeout.Infinite)|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithTestTimeoutEnumAndCooperativeCancellationTrue_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Timeout(TestTimeout.Infinite, CooperativeCancellation = true)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodWithTimeout_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [{|#0:Timeout(5000)|}]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic().WithLocation(0));
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithoutCooperativeCancellation_CodeFixAddsProperty()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(5000)|}]
                public void MyTestMethod()
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
                [Timeout(5000, CooperativeCancellation = true)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0), fixedCode);
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithCooperativeCancellationFalse_CodeFixChangesToTrue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(5000, CooperativeCancellation = false)|}]
                public void MyTestMethod()
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
                [Timeout(5000, CooperativeCancellation = true)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0), fixedCode);
    }

    [TestMethod]
    public async Task WhenTimeoutAttributeWithTestTimeoutEnum_CodeFixAddsProperty()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Timeout(TestTimeout.Infinite)|}]
                public void MyTestMethod()
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
                [Timeout(TestTimeout.Infinite, CooperativeCancellation = true)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0), fixedCode);
    }
}