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
}
