// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.IgnoreShouldHaveJustificationAnalyzer,
    MSTest.Analyzers.IgnoreShouldHaveJustificationFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class IgnoreShouldHaveJustificationAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore("Tracked by #12345")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasIgnoreWithMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore("Disabled until feature ships")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithoutMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore|}]
                public void TestMethod1()
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
                [Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithEmptyMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore("")|}]
                public void TestMethod1()
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
                [Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithWhitespaceMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore("   ")|}]
                public void TestMethod1()
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
                [Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithNullMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            #nullable enable

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore(null)|}]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            #nullable enable

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasIgnoreWithoutMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [{|#0:Ignore|}]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore("TODO: explain why this is ignored")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasIgnoreWithEmptyMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [{|#0:Ignore("")|}]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore("TODO: explain why this is ignored")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"), fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasIgnore_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Ignore]
                public void RegularMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNonTestClassHasIgnore_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [Ignore]
            public class MyClass
            {
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithCombinedAttributes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod, {|#0:Ignore|}]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod, Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithNamedIgnoreMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore(IgnoreMessage = "Tracked by #12345")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestClassHasIgnoreWithNamedIgnoreMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore(IgnoreMessage = "Disabled until feature ships")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithEmptyPositionalAndValidNamedIgnoreMessage_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore("", IgnoreMessage = "Tracked by #12345")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithEmptyNamedIgnoreMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore(IgnoreMessage = "")|}]
                public void TestMethod1()
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
                [Ignore(IgnoreMessage = "TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithWhitespaceNamedIgnoreMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore(IgnoreMessage = "   ")|}]
                public void TestMethod1()
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
                [Ignore(IgnoreMessage = "TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasIgnoreWithNullNamedIgnoreMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            #nullable enable

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [{|#0:Ignore(IgnoreMessage = null)|}]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            #nullable enable

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Ignore(IgnoreMessage = "TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenTestClassHasIgnoreWithEmptyNamedIgnoreMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [{|#0:Ignore(IgnoreMessage = "")|}]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            [Ignore(IgnoreMessage = "TODO: explain why this is ignored")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"), fixedCode);
    }

    [TestMethod]
    public async Task WhenDerivedTestMethodAttributeHasIgnoreWithoutMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                [{|#0:Ignore|}]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [MyTestMethod]
                [Ignore("TODO: explain why this is ignored")]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("TestMethod1"), fixedCode);
    }

    [TestMethod]
    public async Task WhenDerivedTestClassAttributeHasIgnoreWithoutMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClassAttribute : TestClassAttribute { }

            [MyTestClass]
            [{|#0:Ignore|}]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyTestClassAttribute : TestClassAttribute { }

            [MyTestClass]
            [Ignore("TODO: explain why this is ignored")]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, VerifyCS.Diagnostic().WithLocation(0).WithArguments("MyTestClass"), fixedCode);
    }
}
