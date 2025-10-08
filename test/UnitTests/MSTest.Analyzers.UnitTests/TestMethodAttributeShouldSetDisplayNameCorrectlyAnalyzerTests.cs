// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzer,
    MSTest.Analyzers.TestMethodAttributeShouldSetDisplayNameCorrectlyFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class TestMethodAttributeShouldSetDisplayNameCorrectlyAnalyzerTests
{
    [TestMethod]
    public async Task WhenTestMethodHasNoArguments_Attribute_NoDiagnostic()
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

    [TestMethod]
    public async Task WhenTestMethodHasNoArguments_ObjectCreation_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    _ = new TestMethodAttribute();
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasDisplayNameProperty_Attribute_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod(DisplayName = "My Test Name")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasDisplayNameProperty_ObjectCreation_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    _ = new TestMethodAttribute() { DisplayName = "My Test Name" };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringArgument_Attribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod([|"My Test Name"|])]
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
                [TestMethod(DisplayName = "My Test Name")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringArgument_ObjectCreation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    _ = new TestMethodAttribute([|"My Test Name"|]);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    _ = new TestMethodAttribute() { DisplayName = "My Test Name" };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringArgumentWithOtherParameters_Attribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod([|"My Test Name"|], UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto)]
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
                [TestMethod(DisplayName = "My Test Name", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringArgumentWithOtherParameters_ObjectCreation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    // Single line, no trailing comma.
                    _ = new TestMethodAttribute([|"My Test Name"|]) { UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto };

                    // Single line, with trailing comma.
                    _ = new TestMethodAttribute([|"My Test Name"|]) { UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto, };

                    // Multi-line, no trailing comma.
                    _ = new TestMethodAttribute([|"My Test Name"|])
                    {
                        UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto
                    };

                    // Multi-line, with trailing comma.
                    _ = new TestMethodAttribute([|"My Test Name"|])
                    {
                        UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto,
                    };
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class C
            {
                public void M()
                {
                    // Single line, no trailing comma.
                    _ = new TestMethodAttribute() { DisplayName = "My Test Name", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto };

                    // Single line, with trailing comma.
                    _ = new TestMethodAttribute() { DisplayName = "My Test Name", UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto, };

                    // Multi-line, no trailing comma.
                    _ = new TestMethodAttribute()
                    {
                        DisplayName = "My Test Name",
                        UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto
                    };

                    // Multi-line, with trailing comma.
                    _ = new TestMethodAttribute()
                    {
                        DisplayName = "My Test Name",
                        UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto,
                    };
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringArgumentWithSpecialCharacters_Attribute_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod([|"Test with \"quotes\" and \n newlines"|])]
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
                [TestMethod(DisplayName = "Test with \"quotes\" and \n newlines")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenCustomTestMethodAttributeHasStringArgument_Diagnostic()
    {
        string code = """
            using System;
            using System.Runtime.CompilerServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [MyCustomTestMethod([|"My Test Name"|])]
                public void MyTestMethod()
                {
                }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class MyCustomTestMethodAttribute : TestMethodAttribute
            {
                public MyCustomTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Runtime.CompilerServices;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [MyCustomTestMethod(DisplayName = "My Test Name")]
                public void MyTestMethod()
                {
                }
            }

            [AttributeUsage(AttributeTargets.Method)]
            public class MyCustomTestMethodAttribute : TestMethodAttribute
            {
                public MyCustomTestMethodAttribute([CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = 0)
                    : base(callerFilePath, callerLineNumber)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasOnlyUnfoldingStrategyParameter_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Auto)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenMultipleTestMethodsHaveStringArguments_DiagnosticForEach()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod([|"First Test"|])]
                public void FirstTestMethod()
                {
                }

                [TestMethod([|"Second Test"|])]
                public void SecondTestMethod()
                {
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod(DisplayName = "First Test")]
                public void FirstTestMethod()
                {
                }

                [TestMethod(DisplayName = "Second Test")]
                public void SecondTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodHasStringConstantArgument_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                private const string TestName = "My Test Name";

                [TestMethod([|TestName|])]
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
                private const string TestName = "My Test Name";

                [TestMethod(DisplayName = TestName)]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenTestMethodAttributeInAttributeList_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [Description("A test"), TestMethod([|"My Test Name"|])]
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
                [Description("A test"), TestMethod(DisplayName = "My Test Name")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenNonTestMethodHasString_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                public void NonTestMethod(string parameter)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenVerbatimStringUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod([|@"My Test Name with \\ backslashes"|])]
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
                [TestMethod(DisplayName = @"My Test Name with \\ backslashes")]
                public void MyTestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
