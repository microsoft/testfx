// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.StringAssertToAssertAnalyzer,
    MSTest.Analyzers.CodeFixes.StringAssertToAssertFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class StringAssertToAssertAnalyzerTests
{
    [TestMethod]
    public async Task WhenStringAssertContains()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    string substring = "World";
                    {|#0:StringAssert.Contains(value, substring)|};
                    {|#1:StringAssert.Contains(value, substring, "message")|};
                    {|#2:StringAssert.Contains(value, substring, StringComparison.Ordinal)|};
                    {|#3:StringAssert.Contains(value, substring, StringComparison.Ordinal, "message")|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    string substring = "World";
                    Assert.Contains(substring, value);
                    Assert.Contains(substring, value, "message");
                    Assert.Contains(substring, value, StringComparison.Ordinal);
                    Assert.Contains(substring, value, StringComparison.Ordinal, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(12,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "Contains"),
                // /0/Test0.cs(13,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("Contains", "Contains"),
                // /0/Test0.cs(14,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments("Contains", "Contains"),
                // /0/Test0.cs(15,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(3).WithArguments("Contains", "Contains"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenStringAssertStartsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    string substring = "Hello";
                    {|#0:StringAssert.StartsWith(value, substring)|};
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
                    string value = "Hello World";
                    string substring = "Hello";
                    Assert.StartsWith(substring, value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0046: Use 'Assert.StartsWith' instead of 'StringAssert.StartsWith'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("StartsWith", "StartsWith"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenStringAssertEndsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    string substring = "World";
                    {|#0:StringAssert.EndsWith(value, substring)|};
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
                    string value = "Hello World";
                    string substring = "World";
                    Assert.EndsWith(substring, value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0046: Use 'Assert.EndsWith' instead of 'StringAssert.EndsWith'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("EndsWith", "EndsWith"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenStringAssertMatches()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Text.RegularExpressions;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    Regex pattern = new Regex("Hello.*");
                    {|#0:StringAssert.Matches(value, pattern)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Text.RegularExpressions;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    Regex pattern = new Regex("Hello.*");
                    Assert.MatchesRegex(pattern, value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(12,9): info MSTEST0046: Use 'Assert.MatchesRegex' instead of 'StringAssert.Matches'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("MatchesRegex", "Matches"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenStringAssertDoesNotMatch()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Text.RegularExpressions;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    Regex pattern = new Regex("Goodbye.*");
                    {|#0:StringAssert.DoesNotMatch(value, pattern)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Text.RegularExpressions;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    Regex pattern = new Regex("Goodbye.*");
                    Assert.DoesNotMatchRegex(pattern, value);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(12,9): info MSTEST0046: Use 'Assert.DoesNotMatchRegex' instead of 'StringAssert.DoesNotMatch'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotMatchRegex", "DoesNotMatch"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenStringAssertContainsWithMessage()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    string substring = "World";
                    {|#0:StringAssert.Contains(value, substring, "Custom message")|};
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
                    string value = "Hello World";
                    string substring = "World";
                    Assert.Contains(substring, value, "Custom message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "Contains"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNotStringAssertMethod_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string value = "Hello World";
                    Assert.AreEqual("expected", value);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringAssertMethodWithInsufficientArguments_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    // This won't compile but should not crash the analyzer
                    // error CS1501: No overload for method 'Contains' takes 1 arguments
                    StringAssert.{|CS1501:Contains|}("single");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringAssertContains_ShouldPreserveEmptyLines()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void ShouldNotRemoveEmptyLine()
                {
                    WrappedMethodCall(
                        1,
                        2,
                        3);

                    {|#0:StringAssert.Contains("value", "v")|};

                    {|#1:StringAssert.Contains("value", "v")|};

                    {|#2:StringAssert.Contains(
                        "value",
                        "v")|};
                }

                private void WrappedMethodCall(int a, int b, int c) { }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void ShouldNotRemoveEmptyLine()
                {
                    WrappedMethodCall(
                        1,
                        2,
                        3);

                    Assert.Contains("v", "value");

                    Assert.Contains("v", "value");

                    Assert.Contains(
                        "v",
                        "value");
                }

                private void WrappedMethodCall(int a, int b, int c) { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(12,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Contains", "Contains"),
                // /0/Test0.cs(14,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Contains", "Contains"),
                // /0/Test0.cs(16,9): info MSTEST0046: Use 'Assert.Contains' instead of 'StringAssert.Contains'
                VerifyCS.Diagnostic().WithLocation(2).WithArguments("Contains", "Contains"),
            ],
            fixedCode);
    }
}
