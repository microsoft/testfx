// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidOutRefTestMethodParametersAnalyzer,
    MSTest.Analyzers.AvoidOutRefTestMethodParametersFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidOutRefTestMethodParametersAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasOutParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void [|TestMethod1|](out string s, string s2)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void TestMethod1(string s, string s2)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasRefParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void [|TestMethod1|](ref string s, string s2)
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
                [DataRow("Hello", "World")]
                public void TestMethod1(string s, string s2)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasOutAndRefParameters_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void [|TestMethod1|](out string s, ref string s2)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void TestMethod1(string s, string s2)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasOutAndRefParametersOnMultiLine_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void [|TestMethod1|](
                    out string s,
                    ref string s2)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void TestMethod1(
                    string s,
                    string s2)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasNormalParameters_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow("Hello", "World")]
                public void TestMethod1(string s, string s2)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasOutRefParameters_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void HelperMethod(out string s, ref string s2)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenDataTestMethodHasOutParameter_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow("Hello")]
                public void [|TestMethod1|](out string s)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [DataTestMethod]
                [DataRow("Hello")]
                public void TestMethod1(string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasDerivedTestMethodAttributeAndOutParam_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyCustomTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [MyCustomTestMethod]
                public void [|TestMethod1|](out string s)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public sealed class MyCustomTestMethodAttribute : TestMethodAttribute { }

            [TestClass]
            public class MyTestClass
            {
                [MyCustomTestMethod]
                public void TestMethod1(string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodOutsideTestClassHasOutParam_Diagnostic()
    {
        // The analyzer has no [TestClass] guard — it fires on any method carrying a
        // TestMethod-derived attribute, even when the containing class lacks [TestClass].
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClassWithoutTestClass
            {
                [TestMethod]
                public void [|TestMethod1|](out string s)
                {
                    s = "";
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyClassWithoutTestClass
            {
                [TestMethod]
                public void TestMethod1(string s)
                {
                    s = "";
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasInParameter_NoDiagnostic()
    {
        // The analyzer only flags RefKind.Out and RefKind.Ref; 'in' parameters are not flagged.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod1(in string s)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
