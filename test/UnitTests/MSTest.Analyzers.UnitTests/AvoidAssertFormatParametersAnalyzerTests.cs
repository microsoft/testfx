// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertFormatParametersAnalyzer,
    MSTest.Analyzers.CodeFixes.AvoidAssertFormatParametersFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidAssertFormatParametersAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertMethodWithoutFormatParameters_NoDiagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.IsTrue(true);
                    Assert.IsTrue(true, "Simple message");
                    Assert.AreEqual(1, 2);
                    Assert.AreEqual(1, 2, "Simple message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithFormatParameters_Diagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.IsTrue(true, "Value: {0}", 42)|};
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("IsTrue");
        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithFormatParameters_Diagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.IsFalse(false, "Value: {0}", 42)|};
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("IsFalse");
        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithFormatParameters_Diagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual(1, 2, "Expected {0} but got {1}", 1, 2)|};
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual");
        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenCollectionAssertWithFormatParameters_Diagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list1 = new List<int> { 1, 2, 3 };
                    var list2 = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.AreEqual(list1, list2, "Collections differ: {0}", "details")|};
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual");
        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenStringAssertWithFormatParameters_Diagnostic()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:StringAssert.Contains("hello", "world", "String '{0}' not found in '{1}'", "hello", "world")|};
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("Contains");
        await VerifyCS.VerifyAnalyzerAsync(code, expected);
    }

    [TestMethod]
    public async Task WhenAssertWithFormatParameters_FixWithStringFormat()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.IsTrue(true, "Value: {0}", 42)|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.IsTrue(true, string.Format("Value: {0}", 42));
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 0, // Use first code fix (string.Format)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("IsTrue"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenAssertWithFormatParameters_FixWithInterpolatedString()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.IsTrue(true, "Value: {0}", 42)|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.IsTrue(true, $"Value: {42}");
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 1, // Use second code fix (interpolated string)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("IsTrue"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenAssertWithMultipleFormatParameters_FixWithStringFormat()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual(1, 2, "Expected {0} but got {1}", 1, 2)|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual(1, 2, string.Format("Expected {0} but got {1}", 1, 2));
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 0, // Use first code fix (string.Format)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenAssertWithArrayFormatParameters_FixWithStringFormat()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual(1, 2, "Expected {0} but got {1}", new object[] { 1, 2 })|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual(1, 2, string.Format("Expected {0} but got {1}", new object[] { 1, 2 }));
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 0, // Use first code fix (string.Format)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenAssertWithMultipleFormatParameters_FixWithInterpolatedString()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual(1, 2, "Expected {0} but got {1}", 1, 2)|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual(1, 2, $"Expected {1} but got {2}");
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 1, // Use second code fix (interpolated string)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenStringAssertWithFormatParameters_FixWithStringFormat()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:StringAssert.Contains("hello", "world", "String '{0}' not found", "hello")|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    StringAssert.Contains("hello", "world", string.Format("String '{0}' not found", "hello"));
                }
            }
            """;

        await new VerifyCS.Test
        {
            CodeActionIndex = 0, // Use first code fix (string.Format)
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Contains"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenNonStringLiteralFormatString_OnlyStringFormatFix()
    {
        const string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string format = "Value: {0}";
                    {|#0:Assert.IsTrue(true, format, 42)|};
                }
            }
            """;

        const string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string format = "Value: {0}";
                    Assert.IsTrue(true, string.Format(format, 42));
                }
            }
            """;

        DiagnosticResult expected = VerifyCS.Diagnostic().WithLocation(0).WithArguments("IsTrue");
        await VerifyCS.VerifyCodeFixAsync(code, expected, fixedCode);
    }
}
