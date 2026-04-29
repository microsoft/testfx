// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotUseSystemDescriptionAttributeAnalyzer,
    MSTest.Analyzers.DoNotUseSystemDescriptionAttributeFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DoNotUseSystemDescriptionAttributeAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasFullyQualifiedSystemDescriptionAttribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [System.ComponentModel.Description("Description")]
                public void [|MyTestMethod|]()
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
                [Description("Description")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasSystemDescriptionAttributeWithSystemComponentModelUsing_UsesFullyQualifiedMSTestDescription()
    {
        string code = """
            using System.ComponentModel;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Description("Description")]
                public void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            using System.ComponentModel;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Description")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasSystemDescriptionAttributeWithoutUnitTestingUsing_UsesFullyQualifiedMSTestDescription()
    {
        string code = """
            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                [System.ComponentModel.Description("Description")]
                public void [|MyTestMethod|]()
                {
                }
            }
            """;

        string fixedCode = """
            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                [Microsoft.VisualStudio.TestTools.UnitTesting.DescriptionAttribute("Description")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMethodWithoutTestMethodAttribute_HasSystemDescriptionAttribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [System.ComponentModel.Description("Description")]
                public void Method()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
