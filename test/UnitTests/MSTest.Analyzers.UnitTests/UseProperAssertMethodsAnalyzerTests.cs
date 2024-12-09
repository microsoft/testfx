// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseProperAssertMethodsAnalyzer,
    MSTest.Analyzers.UseProperAssertMethodsFixer>;

namespace MSTest.Analyzers.Test;

// NOTE: tests in this class are intentionally not using the [|...|] markup syntax so that we test the arguments
[TestGroup]
public sealed class UseProperAssertMethodsAnalyzerTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    public async Task WhenAssertIsTrueWithEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x == null)|};
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
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsTrueWithIsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x is null)|};
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
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsTrueWithNotEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x != null)|};
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
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsTrueWithIsNotNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsTrue(x is not null)|};
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
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseWithEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x == null)|};
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
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseWithIsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x is null)|};
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
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseWithNotEqualsNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x != null)|};
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
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseWithIsNotNullArgument()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.IsFalse(x is not null)|};
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
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertIsTrueAndArgumentIsEquality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsTrue(x == y)|};
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
                    object x = new object();
                    object y = new object();
                    Assert.AreEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsTrueAndArgumentIsInequality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsTrue(x != y)|};
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
                    object x = new object();
                    object y = new object();
                    Assert.AreNotEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsTrue"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseAndArgumentIsEquality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsFalse(x == y)|};
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
                    object x = new object();
                    object y = new object();
                    Assert.AreNotEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertIsFalseAndArgumentIsInequality()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    object y = new object();
                    {|#0:Assert.IsFalse(x != y)|};
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
                    object x = new object();
                    object y = new object();
                    Assert.AreEqual(y, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsFalse"),
            fixedCode);
    }

    public async Task WhenAssertAreEqualAndExpectedIsNull()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(null, x)|};
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
                    object x = new object();
                    Assert.IsNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreNotEqualAndExpectedIsNull()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreNotEqual(null, x)|};
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
                    object x = new object();
                    Assert.IsNotNull(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.AreNotEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "AreNotEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreEqualAndExpectedIsTrue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(true, x)|};
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
                    object x = new object();
                    Assert.IsTrue((bool?)x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastNotAddedWhenTypeIsBool()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool x = false;
                    {|#0:Assert.AreEqual(true, x)|};
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
                    bool x = false;
                    Assert.IsTrue(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastNotAddedWhenTypeIsNullableBool()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    bool? x = false;
                    {|#0:Assert.AreEqual(true, x)|};
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
                    bool? x = false;
                    Assert.IsTrue(x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreEqualAndExpectedIsTrue_CastShouldBeAddedWithParentheses()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual<object>(true, new C() + new C())|};
                }
            }

            public class C
            {
                public static object operator +(C c1, C c2)
                    => true;
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
                    Assert.IsTrue((bool?)(new C() + new C()));
                }
            }

            public class C
            {
                public static object operator +(C c1, C c2)
                    => true;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsTrue' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsTrue", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreNotEqualAndExpectedIsTrue()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    // Note: Assert.IsFalse(x) has different semantics. So no diagnostic.
                    // We currently don't produce a diagnostic even if the type of 'x' is boolean.
                    // But we could special case that.
                    Assert.AreNotEqual(true, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    public async Task WhenAssertAreEqualAndExpectedIsFalse()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    {|#0:Assert.AreEqual(false, x)|};
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
                    object x = new object();
                    Assert.IsFalse((bool?)x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsFalse' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsFalse", "AreEqual"),
            fixedCode);
    }

    public async Task WhenAssertAreNotEqualAndExpectedIsFalse()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object x = new object();
                    // Note: Assert.IsTrue(x) has different semantics. So no diagnostic.
                    // We currently don't produce a diagnostic even if the type of 'x' is boolean.
                    // But we could special case that.
                    Assert.AreNotEqual(false, x);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
