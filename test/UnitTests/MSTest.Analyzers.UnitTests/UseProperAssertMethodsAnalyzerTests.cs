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
}
