// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DoNotNegateBooleanAssertionAnalyzer,
    MSTest.Analyzers.DoNotNegateBooleanAssertionFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DoNotNegateBooleanAssertionAnalyzerTests
{
    [TestMethod]
    public async Task WhenAssertionIsNotNegated_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool b = true;

                    Assert.IsTrue(true);
                    Assert.IsTrue(false);
                    Assert.IsTrue(b);
                    Assert.IsTrue(GetBoolean());

                    Assert.IsFalse(true);
                    Assert.IsFalse(false);
                    Assert.IsFalse(b);
                    Assert.IsFalse(GetBoolean());
                }

                private bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertionIsNegated_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool b = true;
            
                    [|Assert.IsTrue(!true)|];
                    [|Assert.IsTrue(!false)|];
                    [|Assert.IsTrue(!b)|];
                    [|Assert.IsTrue(!GetBoolean())|];
                    [|Assert.IsTrue(!(GetBoolean()))|];
                    [|Assert.IsTrue((!(GetBoolean())))|];

                    [|Assert.IsFalse(!true)|];
                    [|Assert.IsFalse(!false)|];
                    [|Assert.IsFalse(!b)|];
                    [|Assert.IsFalse(!GetBoolean())|];
                    [|Assert.IsFalse(!(GetBoolean()))|];
                    [|Assert.IsFalse((!(GetBoolean())))|];
                }

                private bool GetBoolean() => true;
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool b = true;
            
                    Assert.IsFalse(true);
                    Assert.IsFalse(false);
                    Assert.IsFalse(b);
                    Assert.IsFalse(GetBoolean());
                    Assert.IsFalse(GetBoolean());
                    Assert.IsFalse(GetBoolean());

                    Assert.IsTrue(true);
                    Assert.IsTrue(false);
                    Assert.IsTrue(b);
                    Assert.IsTrue(GetBoolean());
                    Assert.IsTrue(GetBoolean());
                    Assert.IsTrue(GetBoolean());
                }

                private bool GetBoolean() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithComplexNegatedExpression_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool a = true;
                    bool b = false;
                    [|Assert.IsTrue(!(a && b))|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool a = true;
                    bool b = false;
                    Assert.IsFalse(a && b);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegatedMethodCall_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(!IsValid())|];
                }

                private bool IsValid() => true;
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(IsValid());
                }

                private bool IsValid() => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegatedProperty_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var obj = new TestObject();
                    [|Assert.IsTrue(!obj.IsEnabled)|];
                }
            }

            public class TestObject
            {
                public bool IsEnabled { get; set; }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var obj = new TestObject();
                    Assert.IsFalse(obj.IsEnabled);
                }
            }

            public class TestObject
            {
                public bool IsEnabled { get; set; }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithParenthesizedNegation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    [|Assert.IsTrue((!condition))|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    Assert.IsFalse(condition);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithMessage_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    [|Assert.IsTrue(!condition, "Condition should be false")|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    Assert.IsFalse(condition, "Condition should be false");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithoutNegation_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    Assert.IsTrue(condition);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithoutNegation_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = false;
                    Assert.IsFalse(condition);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithDoubleNegation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    [|Assert.IsTrue(!!condition)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    Assert.IsTrue(condition);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithTripleNegation_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    [|Assert.IsTrue(!!!condition)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    Assert.IsFalse(condition);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMultipleNegatedAssertions_FixAll()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition1 = true;
                    bool condition2 = false;
                    bool condition3 = true;
                    
                    [|Assert.IsTrue(!condition1)|];
                    [|Assert.IsFalse(!condition2)|];
                    [|Assert.IsTrue(!condition3)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition1 = true;
                    bool condition2 = false;
                    bool condition3 = true;
                    
                    Assert.IsFalse(condition1);
                    Assert.IsTrue(condition2);
                    Assert.IsFalse(condition3);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegatedComparison_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 5;
                    int y = 10;
                    [|Assert.IsTrue(!(x < y))|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 5;
                    int y = 10;
                    Assert.IsFalse(x < y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithNegatedEquality_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 5;
                    int y = 5;
                    [|Assert.IsFalse(!(x == y))|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 5;
                    int y = 5;
                    Assert.IsTrue(x == y);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegatedNullCheck_Diagnostic()
    {
        string code = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    object? obj = null;
                    [|Assert.IsTrue(!(obj == null))|];
                }
            }
            """;

        string fixedCode = """
            #nullable enable
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    object? obj = null;
                    Assert.IsFalse(obj == null);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithTernaryOperator_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int x = 5;
                    Assert.IsTrue(x > 0 ? true : false);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenNonAssertMethodWithNegation_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    bool condition = true;
                    CustomAssert.IsTrue(!condition);
                }
            }

            public static class CustomAssert
            {
                public static void IsTrue(bool condition) { }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegation_PreservesMultilineFormatting()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(!false, "some explanation")|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(false, "some explanation");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNegation_PreservesComments()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.IsTrue(/* some comment */ !false /* some other comment */)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.IsFalse(/* some comment */ false /* some other comment */);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }
}
