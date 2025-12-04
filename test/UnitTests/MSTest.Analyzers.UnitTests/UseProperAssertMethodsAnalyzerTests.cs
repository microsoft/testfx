// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseProperAssertMethodsAnalyzer,
    MSTest.Analyzers.UseProperAssertMethodsFixer>;

namespace MSTest.Analyzers.Test;

// NOTE: tests in this class are intentionally not using the [|...|] markup syntax so that we test the arguments
[TestClass]
public sealed class UseProperAssertMethodsAnalyzerTests
{
    private const string SomeClassWithUserDefinedEqualityOperators = """
        public class SomeClass
        {
            public static bool operator ==(SomeClass x, SomeClass y) => true;
            public static bool operator !=(SomeClass x, SomeClass y) => false;
        }
        """;

    [TestMethod]
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

    [TestMethod]
    public async Task WhenPointerTypesPassedToIsTrueOrIsFalseThenNoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public unsafe void MyTestMethod()
                {
                    {
                        byte* x = null;
                        delegate*<void> y = null;

                        // Assert.IsTrue: null comparisons
                        Assert.IsTrue(x is null);
                        Assert.IsTrue(y is null);
                        Assert.IsTrue(x == null);
                        Assert.IsTrue(y == null);
                        Assert.IsTrue(null == x);
                        Assert.IsTrue(null == y);

                        // Assert.IsTrue: not null comparisons
                        Assert.IsTrue(x is not null);
                        Assert.IsTrue(y is not null);
                        Assert.IsTrue(x != null);
                        Assert.IsTrue(y != null);
                        Assert.IsTrue(null != x);
                        Assert.IsTrue(null != y);

                        // Assert.IsTrue: two pointers equality comparisons
                        Assert.IsTrue(x == x);
                        Assert.IsTrue(x == y);
                        Assert.IsTrue(y == x);
                        Assert.IsTrue(y == y);

                        // Assert.IsTrue: two pointers inequality comparisons
                        Assert.IsTrue(x != x);
                        Assert.IsTrue(x != y);
                        Assert.IsTrue(y != x);
                        Assert.IsTrue(y != y);

                        // Assert.IsFalse: null comparisons
                        Assert.IsFalse(x is null);
                        Assert.IsFalse(y is null);
                        Assert.IsFalse(x == null);
                        Assert.IsFalse(y == null);
                        Assert.IsFalse(null == x);
                        Assert.IsFalse(null == y);

                        // Assert.IsFalse: not null comparisons
                        Assert.IsFalse(x is not null);
                        Assert.IsFalse(y is not null);
                        Assert.IsFalse(x != null);
                        Assert.IsFalse(y != null);
                        Assert.IsFalse(null != x);
                        Assert.IsFalse(null != y);

                        // Assert.IsFalse: two pointers equality comparisons
                        Assert.IsFalse(x == x);
                        Assert.IsFalse(x == y);
                        Assert.IsFalse(y == x);
                        Assert.IsFalse(y == y);

                        // Assert.IsFalse: two pointers inequality comparisons
                        Assert.IsFalse(x != x);
                        Assert.IsFalse(x != y);
                        Assert.IsFalse(y != x);
                        Assert.IsFalse(y != y);

                        // The following is to show that if we try to use IsNull or AreEqual, it will not work.
                        // If this behavior ever changed (either as a result of some language feature, or us adding extra overloads),
                        // then we should update the analyzer to start reporting diagnostic in the relevant cases and have the codefix handle it correctly
                        Assert.IsNull({|#0:x|});
                        Assert.IsNull({|#1:y|});
                        Assert.{|#2:AreEqual|}(null, x);
                        Assert.{|#3:AreEqual|}(null, y);
                    }
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(15,27): error CS1503: Argument 1: cannot convert from 'byte*' to 'object?'
                DiagnosticResult.CompilerError("CS1503").WithLocation(0).WithArguments("1", "byte*", "object?"),
                // /0/Test0.cs(16,27): error CS1503: Argument 1: cannot convert from 'delegate*<void>' to 'object?'
                DiagnosticResult.CompilerError("CS1503").WithLocation(1).WithArguments("1", "delegate*<void>", "object?"),
                // /0/Test0.cs(17,20): error CS0306: The type 'byte*' may not be used as a type argument
                DiagnosticResult.CompilerError("CS0306").WithLocation(2).WithArguments("byte*"),
                // /0/Test0.cs(18,20): error CS0306: The type 'delegate*<void>' may not be used as a type argument
                DiagnosticResult.CompilerError("CS0306").WithLocation(3).WithArguments("delegate*<void>"),
            ],
            code);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsTrue(x == null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsTrue(x is null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueWithNotEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsTrue(x != null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueWithIsNotNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsTrue(x is not null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNotNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseWithEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsFalse(x == null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsFalse(x is null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNotNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseWithNotEqualsNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsFalse(x != null);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseWithIsNotNullArgumentAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    {|#0:Assert.IsFalse(x is not null)|};
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        string fixedCode = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    Assert.IsNull(x);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNull' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNull", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsEqualityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsTrue(x == y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueAndArgumentIsInequalityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsTrue(x != y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsEqualityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsFalse(x == y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsFalseAndArgumentIsInequalityAndUserDefinedOperator()
    {
        string code = $$"""
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    SomeClass x = new SomeClass();
                    SomeClass y = new SomeClass();
                    Assert.IsFalse(x != y);
                }
            }

            {{SomeClassWithUserDefinedEqualityOperators}}
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public async Task WhenAssertIsTrueOrIsFalseWithWrongContainsMethod()
    {
        string code = """
            using System.Collections.ObjectModel;
            using System.IO;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTests
            {
                [TestMethod]
                public void Contains()
                {
                    // This collection is KeyedCollection<TKey, TItem>
                    // It implements IEnumerable<TItem>, but the available Contains method searches for TKey.
                    // Whether or not the type of TKey and TItem match, we shouldn't raise a diagnostic as it changes semantics.
                    var collection = new MyKeyedCollection();
                    Assert.IsFalse(collection.Contains(5));
                    Assert.IsTrue(collection.Contains(5));
                }

                internal class MyKeyedCollection : KeyedCollection<int, int>
                {
                    protected override int GetKeyForItem(int item)
                    {
                        return 667;
                    }
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    #region New test cases for string methods

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringStartsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.StartsWith("Hello"))|};
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
                    string myString = "Hello World";
                    Assert.StartsWith("Hello", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.StartsWith' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("StartsWith", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringEndsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.EndsWith("World"))|};
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
                    string myString = "Hello World";
                    Assert.EndsWith("World", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.EndsWith' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("EndsWith", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithStringContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsTrue(myString.Contains("lo Wo"))|};
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
                    string myString = "Hello World";
                    Assert.Contains("lo Wo", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.Contains' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringStartsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsFalse(myString.StartsWith("Hello"))|};
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
                    string myString = "Hello World";
                    Assert.DoesNotStartWith("Hello", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.DoesNotStartWith' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotStartWith", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringEndsWith()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsFalse(myString.EndsWith("World"))|};
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
                    string myString = "Hello World";
                    Assert.DoesNotEndWith("World", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.DoesNotEndWith' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotEndWith", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithStringContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string myString = "Hello World";
                    {|#0:Assert.IsFalse(myString.Contains("test"))|};
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
                    string myString = "Hello World";
                    Assert.DoesNotContain("test", myString);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.DoesNotContain' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
            fixedCode);
    }

    #endregion

    #region New test cases for collection methods

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Contains(2))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.Contains(2, list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.Contains' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithCollectionContains()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsFalse(list.Contains(4))|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.DoesNotContain(4, list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.DoesNotContain' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
            fixedCode);
    }

    #endregion

    #region New test cases for comparisons

    [TestMethod]
    public async Task WhenAssertIsTrueWithGreaterThanComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsTrue(a > b)|};
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
                    int a = 5;
                    int b = 3;
                    Assert.IsGreaterThan(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithGreaterThanComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsFalse(a > b)|};
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
                    int a = 5;
                    int b = 3;
                    Assert.IsLessThanOrEqualTo(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsLessThanOrEqualTo' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsLessThanOrEqualTo", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithEqualsComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 5;
                    {|#0:Assert.IsTrue(a == b)|};
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
                    int a = 5;
                    int b = 5;
                    Assert.AreEqual(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreEqual", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithEqualsComparison()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int a = 5;
                    int b = 3;
                    {|#0:Assert.IsFalse(a == b)|};
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
                    int a = 5;
                    int b = 3;
                    Assert.AreNotEqual(b, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsFalse"),
            fixedCode);
    }

    #endregion

    #region New test cases for collection count

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    {|#0:Assert.AreEqual(0, list.Count)|};
                    Assert.AreEqual(list.Count, 0);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.IsEmpty(list);
                    Assert.AreEqual(list.Count, 0);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.AreEqual'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "AreEqual"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountNonZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    int x = 3;
                    {|#0:Assert.AreEqual(3, list.Count)|};
                    Assert.AreEqual(list.Count, 3);
                    {|#1:Assert.AreEqual(x, list.Count)|};
                    Assert.AreEqual(list.Count, x);
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    int x = 3;
                    Assert.HasCount(3, list);
                    Assert.AreEqual(list.Count, 3);
                    Assert.HasCount(x, list);
                    Assert.AreEqual(list.Count, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
                // /0/Test0.cs(14,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("HasCount", "AreEqual"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithCollectionCountZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.AreNotEqual(0, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithCollectionCountNonZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.AreNotEqual(3, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreNotEqualWithArrayLength()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var array = new int[] { 1, 2, 3, 4, 5 };
                    Assert.AreNotEqual(5, array.Length);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountAndMessage_HasCount()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var myCollection = new List<int> { 1, 2 };
                    {|#0:Assert.AreEqual(2, myCollection.Count, "Wrong number of elements")|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var myCollection = new List<int> { 1, 2 };
                    Assert.HasCount(2, myCollection, "Wrong number of elements");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountZeroAndMessage_IsEmpty()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    {|#0:Assert.AreEqual(0, list.Count, "Collection should be empty")|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int>();
                    Assert.IsEmpty(list, "Collection should be empty");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountUsingCustomCollection()
    {
        string code = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyCustomCollection<T> : IEnumerable<T>
            {
                public IEnumerator<T> GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                public int Count => 5;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var x = new MyCustomCollection<string>();
                    Assert.AreEqual(4, x.Count);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithCollectionCountUsingNonGenericCollection()
    {
        string code = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var x = new Hashtable();
                    {|#0:Assert.AreEqual(4, x.Count)|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var x = new Hashtable();
                    Assert.HasCount(4, x);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(13,9): info MSTEST0037: Use 'Assert.HasCount' instead of 'Assert.AreEqual'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("HasCount", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertAreEqualWithNonGenericCollectionCountZero()
    {
        string code = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable();
                    {|#0:Assert.AreEqual(0, hashtable.Count)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable();
                    Assert.IsEmpty(hashtable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "AreEqual"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNonGenericCollectionCountEqualZero()
    {
        string code = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable();
                    {|#0:Assert.IsTrue(hashtable.Count == 0)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable();
                    Assert.IsEmpty(hashtable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNonGenericCollectionCountGreaterThanZero()
    {
        string code = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable { { "key", "value" } };
                    {|#0:Assert.IsTrue(hashtable.Count > 0)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var hashtable = new Hashtable { { "key", "value" } };
                    Assert.IsNotEmpty(hashtable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }
    #endregion

    #region New test cases for collection emptiness checks

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionCountGreaterThanZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Count > 0)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsNotEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionCountNotEqualToZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Count != 0)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsNotEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionCountEqualsZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Count == 0)|};
                    {|#1:Assert.IsTrue(0 == list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsEmpty(list);
                    Assert.IsEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "IsTrue"),
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsEmpty", "IsTrue"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithArrayLengthGreaterThanZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var array = new int[] { 1, 2, 3 };
                    {|#0:Assert.IsTrue(array.Length > 0)|};
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
                    var array = new int[] { 1, 2, 3 };
                    Assert.IsNotEmpty(array);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithArrayLengthNotEqualToZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var array = new int[] { 1, 2, 3 };
                    {|#0:Assert.IsTrue(array.Length != 0)|};
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
                    var array = new int[] { 1, 2, 3 };
                    Assert.IsNotEmpty(array);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithArrayLengthEqualToZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var array = new int[] { 1, 2, 3 };
                    {|#0:Assert.IsTrue(array.Length == 0)|};
                    {|#1:Assert.IsTrue(0 == array.Length)|};
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
                    var array = new int[] { 1, 2, 3 };
                    Assert.IsEmpty(array);
                    Assert.IsEmpty(array);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(10,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "IsTrue"),
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsEmpty", "IsTrue"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithZeroNotEqualToCollectionCount()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(0 != list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsNotEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithCollectionCountGreaterThanZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsFalse(list.Count > 0)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithCollectionCountEqualZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsFalse(list.Count == 0)|};
                    {|#1:Assert.IsFalse(0 == list.Count)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsNotEmpty(list);
                    Assert.IsNotEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotEmpty", "IsFalse"),
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.IsNotEmpty' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsNotEmpty", "IsFalse"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithCollectionCountNotEqualToZero()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsFalse(list.Count != 0)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.IsEmpty(list);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsEmpty' instead of 'Assert.IsFalse'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsEmpty", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionCountGreaterThanNonZero_ShouldUseIsGreaterThan()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    // This should use the generic comparison logic, not IsNotEmpty
                    {|#0:Assert.IsTrue(list.Count > 2)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    // This should use the generic comparison logic, not IsNotEmpty
                    Assert.IsGreaterThan(2, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithCollectionCountNotEqualToNonZero_ShouldUseAreNotEqual()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    {|#0:Assert.IsTrue(list.Count != 5)|};
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var list = new List<int> { 1, 2, 3 };
                    Assert.AreNotEqual(5, list.Count);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotEqual", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithNonBCLCollectionCount_ShouldUseGenericComparison()
    {
        string code = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyCustomCollection<T> : IEnumerable<T>
            {
                public IEnumerator<T> GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                public int Count => 5;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var customCollection = new MyCustomCollection<string>();
                    // This should use the generic comparison logic since it's not a BCL collection
                    {|#0:Assert.IsTrue(customCollection.Count > 0)|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            internal sealed class MyCustomCollection<T> : IEnumerable<T>
            {
                public IEnumerator<T> GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    throw new NotImplementedException();
                }

                public int Count => 5;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var customCollection = new MyCustomCollection<string>();
                    // This should use the generic comparison logic since it's not a BCL collection
                    Assert.IsGreaterThan(0, customCollection.Count);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            // /0/Test0.cs(25,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
            fixedCode);
    }

    #endregion

    #region Predicate Pattern Tests
    [TestMethod]
    public async Task WhenUsingIsTrueAnyWithPredicate_SuggestsContains()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsTrue(enumerable.Any(x => x == 1))|};
                }
            }
            
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.Contains(x => x == 1, enumerable);
                }
            }
            
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsTrueWhereAnyWithPredicate_SuggestsContains()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsTrue(enumerable.Where(x => x == 1).Any())|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.Contains(x => x == 1, enumerable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsFalseWhereAnyWithPredicate_SuggestsDoesNotContain()
    {
        string code = """
            
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsFalse(enumerable.Where(x => x == 1).Any())|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsFalseWithAny_SuggestsDoesNotContain()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsFalse(enumerable.Any(x => x == 1))|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
           code,
           VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
           fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsFalseWithWhereAny_SuggestsDoesNotContain()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsFalse(enumerable.Where(x => x == 1).Any())|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsTrueCountGreaterThanZero_SuggestsContains()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsTrue(enumerable.Count(x => x == 1) > 0)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.Contains(x => x == 1, enumerable);
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "IsTrue"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsFalseCountGreaterThanZero_SuggestsDoesNotContain()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsFalse(enumerable.Count(x => x == 1) > 0)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
            """;
        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsFalse"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNotNullSingleOrDefaultWithPredicate_SuggestsContainsSingle()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNotNull(enumerable.SingleOrDefault(x => x == 1))|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.ContainsSingle(x => x == 1, enumerable);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("ContainsSingle", "IsNotNull"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNotNullSingleWithPredicate_SuggestsContainsSingle()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNotNull(enumerable.Single(x => x == 1))|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.ContainsSingle(x => x == 1, enumerable);
                }
            }
        
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("ContainsSingle", "IsNotNull"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNotNullWhereSingleOrDefault_SuggestsContainsSingle()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNotNull(enumerable.Where(x => x == 1).SingleOrDefault())|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.ContainsSingle(x => x == 1, enumerable);
                }
            }
        
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("ContainsSingle", "IsNotNull"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNotNullWhereSingle_SuggestsContainsSingle()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNotNull(enumerable.Where(x => x == 1).Single())|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.ContainsSingle(x => x == 1, enumerable);
                }
            }
        
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("ContainsSingle", "IsNotNull"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNullSingleOrDefaultWithPredicate_SuggestsDoesNotContain()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNull(enumerable.SingleOrDefault(x => x == 1))|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
        
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsNull"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenUsingIsNullWhereSingleOrDefault_SuggestsDoesNotContain()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    {|#0:Assert.IsNull(enumerable.Where(x => x == 1).SingleOrDefault())|};
                }
            }
        
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class TestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var enumerable = new List<int>();
                    Assert.DoesNotContain(x => x == 1, enumerable);
                }
            }
        
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "IsNull"),
            fixedCode);
    }

    #endregion

    #region BCL Types with IComparable Tests

    [TestMethod]
    public async Task WhenAssertIsTrueWithTimeSpanComparison()
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
                    var ts1 = TimeSpan.Zero;
                    var ts2 = TimeSpan.FromSeconds(1);
                    {|#0:Assert.IsTrue(ts2 > ts1)|};
                    {|#1:Assert.IsTrue(ts2 >= ts1)|};
                    {|#2:Assert.IsTrue(ts1 < ts2)|};
                    {|#3:Assert.IsTrue(ts1 <= ts2)|};
                    {|#4:Assert.IsTrue(ts1 == ts1)|};
                    {|#5:Assert.IsTrue(ts1 != ts2)|};
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
                    var ts1 = TimeSpan.Zero;
                    var ts2 = TimeSpan.FromSeconds(1);
                    Assert.IsGreaterThan(ts1, ts2);
                    Assert.IsGreaterThanOrEqualTo(ts1, ts2);
                    Assert.IsLessThan(ts2, ts1);
                    Assert.IsLessThanOrEqualTo(ts2, ts1);
                    Assert.AreEqual(ts1, ts1);
                    Assert.AreNotEqual(ts2, ts1);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.IsGreaterThanOrEqualTo' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsGreaterThanOrEqualTo", "IsTrue"),
                // /0/Test0.cs(13,9): info MSTEST0037: Use 'Assert.IsLessThan' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments("IsLessThan", "IsTrue"),
                // /0/Test0.cs(14,9): info MSTEST0037: Use 'Assert.IsLessThanOrEqualTo' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(3).WithArguments("IsLessThanOrEqualTo", "IsTrue"),
                // /0/Test0.cs(15,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(4).WithArguments("AreEqual", "IsTrue"),
                // /0/Test0.cs(16,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(5).WithArguments("AreNotEqual", "IsTrue"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsTrueWithDateTimeComparison()
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
                    var dt1 = DateTime.Today;
                    var dt2 = DateTime.Today.AddDays(1);
                    {|#0:Assert.IsTrue(dt2 > dt1)|};
                    {|#1:Assert.IsTrue(dt2 >= dt1)|};
                    {|#2:Assert.IsTrue(dt1 < dt2)|};
                    {|#3:Assert.IsTrue(dt1 <= dt2)|};
                    {|#4:Assert.IsTrue(dt1 == dt1)|};
                    {|#5:Assert.IsTrue(dt1 != dt2)|};
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
                    var dt1 = DateTime.Today;
                    var dt2 = DateTime.Today.AddDays(1);
                    Assert.IsGreaterThan(dt1, dt2);
                    Assert.IsGreaterThanOrEqualTo(dt1, dt2);
                    Assert.IsLessThan(dt2, dt1);
                    Assert.IsLessThanOrEqualTo(dt2, dt1);
                    Assert.AreEqual(dt1, dt1);
                    Assert.AreNotEqual(dt2, dt1);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsGreaterThan", "IsTrue"),
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.IsGreaterThanOrEqualTo' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsGreaterThanOrEqualTo", "IsTrue"),
                // /0/Test0.cs(13,9): info MSTEST0037: Use 'Assert.IsLessThan' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments("IsLessThan", "IsTrue"),
                // /0/Test0.cs(14,9): info MSTEST0037: Use 'Assert.IsLessThanOrEqualTo' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(3).WithArguments("IsLessThanOrEqualTo", "IsTrue"),
                // /0/Test0.cs(15,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(4).WithArguments("AreEqual", "IsTrue"),
                // /0/Test0.cs(16,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsTrue'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(5).WithArguments("AreNotEqual", "IsTrue"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenAssertIsFalseWithTimeSpanComparison()
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
                    var ts1 = TimeSpan.Zero;
                    var ts2 = TimeSpan.FromSeconds(1);
                    {|#0:Assert.IsFalse(ts2 > ts1)|};
                    {|#1:Assert.IsFalse(ts2 >= ts1)|};
                    {|#2:Assert.IsFalse(ts1 < ts2)|};
                    {|#3:Assert.IsFalse(ts1 <= ts2)|};
                    {|#4:Assert.IsFalse(ts1 == ts1)|};
                    {|#5:Assert.IsFalse(ts1 != ts2)|};
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
                    var ts1 = TimeSpan.Zero;
                    var ts2 = TimeSpan.FromSeconds(1);
                    Assert.IsLessThanOrEqualTo(ts1, ts2);
                    Assert.IsLessThan(ts1, ts2);
                    Assert.IsGreaterThanOrEqualTo(ts2, ts1);
                    Assert.IsGreaterThan(ts2, ts1);
                    Assert.AreNotEqual(ts1, ts1);
                    Assert.AreEqual(ts2, ts1);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                // /0/Test0.cs(11,9): info MSTEST0037: Use 'Assert.IsLessThanOrEqualTo' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsLessThanOrEqualTo", "IsFalse"),
                // /0/Test0.cs(12,9): info MSTEST0037: Use 'Assert.IsLessThan' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsLessThan", "IsFalse"),
                // /0/Test0.cs(13,9): info MSTEST0037: Use 'Assert.IsGreaterThanOrEqualTo' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments("IsGreaterThanOrEqualTo", "IsFalse"),
                // /0/Test0.cs(14,9): info MSTEST0037: Use 'Assert.IsGreaterThan' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(3).WithArguments("IsGreaterThan", "IsFalse"),
                // /0/Test0.cs(15,9): info MSTEST0037: Use 'Assert.AreNotEqual' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(4).WithArguments("AreNotEqual", "IsFalse"),
                // /0/Test0.cs(16,9): info MSTEST0037: Use 'Assert.AreEqual' instead of 'Assert.IsFalse'
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(5).WithArguments("AreEqual", "IsFalse"),
            ],
            fixedCode);
    }

    #endregion

    [TestMethod]
    public async Task WhenAssertIsTrueWithUserDefinedComparisonOperatorsThenNoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class MyCustomType
            {
                public static bool operator >(MyCustomType x, MyCustomType y) => true;
                public static bool operator <(MyCustomType x, MyCustomType y) => false;
                public static bool operator >=(MyCustomType x, MyCustomType y) => true;
                public static bool operator <=(MyCustomType x, MyCustomType y) => false;
            }

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new MyCustomType();
                    var b = new MyCustomType();
                    // These should NOT trigger diagnostics because they're user-defined operators from non-BCL types
                    Assert.IsTrue(a > b);
                    Assert.IsTrue(a >= b);
                    Assert.IsTrue(a < b);
                    Assert.IsTrue(a <= b);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, code);
    }
}
