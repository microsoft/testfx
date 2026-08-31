// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertAreSameWithValueTypesAnalyzer,
    MSTest.Analyzers.AvoidAssertAreSameWithValueTypesFixer>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidAssertAreSameWithValueTypesAnalyzerTests
{
    [TestMethod]
    public async Task UseAssertAreSameWithValueTypes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    [|Assert.AreSame(0, 0)|];
                    [|Assert.AreSame(0, 0, "Message")|];
                    [|Assert.AreSame(message: "Message", expected: 0, actual: 0)|];

                    [|Assert.AreSame<object>(0, 0)|];
                    [|Assert.AreSame<object>(0, 0, "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: 0)|];

                    // Expected is value type. This is always-failing assert.
                    [|Assert.AreSame<object>("0", 0)|];
                    [|Assert.AreSame<object>("0", 0, "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: "0", actual: 0)|];

                    // Actual is value type. This is always-failing assert.
                    [|Assert.AreSame<object>(0, "0")|];
                    [|Assert.AreSame<object>(0, "0", "Message")|];
                    [|Assert.AreSame<object>(message: "Message", expected: 0, actual: "0")|];

                    // Both are reference types. No diagnostic.
                    Assert.AreSame("0", "0");
                    Assert.AreSame("0", "0", "Message");
                    Assert.AreSame(message: "Message", expected: "0", actual: "0");
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
                public void TestMethod()
                {
                    // Both are value types
                    Assert.AreEqual(0, 0);
                    Assert.AreEqual(0, 0, "Message");
                    Assert.AreEqual(message: "Message", expected: 0, actual: 0);

                    Assert.AreEqual<object>(0, 0);
                    Assert.AreEqual<object>(0, 0, "Message");
                    Assert.AreEqual<object>(message: "Message", expected: 0, actual: 0);

                    // Expected is value type. This is always-failing assert.
                    Assert.AreEqual<object>("0", 0);
                    Assert.AreEqual<object>("0", 0, "Message");
                    Assert.AreEqual<object>(message: "Message", expected: "0", actual: 0);

                    // Actual is value type. This is always-failing assert.
                    Assert.AreEqual<object>(0, "0");
                    Assert.AreEqual<object>(0, "0", "Message");
                    Assert.AreEqual<object>(message: "Message", expected: 0, actual: "0");

                    // Both are reference types. No diagnostic.
                    Assert.AreSame("0", "0");
                    Assert.AreSame("0", "0", "Message");
                    Assert.AreSame(message: "Message", expected: "0", actual: "0");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task UseAssertAreNotSameWithValueTypes_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    // Both are value types
                    [|Assert.AreNotSame(0, 1)|];
                    [|Assert.AreNotSame(0, 1, "Message")|];
                    [|Assert.AreNotSame(message: "Message", notExpected: 0, actual: 1)|];

                    [|Assert.AreNotSame<object>(0, 1)|];
                    [|Assert.AreNotSame<object>(0, 1, "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: 0, actual: 1)|];

                    // Expected is value type. This is always-failing assert.
                    [|Assert.AreNotSame<object>("0", 1)|];
                    [|Assert.AreNotSame<object>("0", 1, "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: "0", actual: 1)|];

                    // Actual is value type. This is always-failing assert.
                    [|Assert.AreNotSame<object>(0, "1")|];
                    [|Assert.AreNotSame<object>(0, "1", "Message")|];
                    [|Assert.AreNotSame<object>(message: "Message", notExpected: 0, actual: "1")|];

                    // Both are reference types. No diagnostic.
                    Assert.AreNotSame("0", "1");
                    Assert.AreNotSame("0", "1", "Message");
                    Assert.AreNotSame(message: "Message", notExpected: "0", actual: "1");
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
                public void TestMethod()
                {
                    // Both are value types
                    Assert.AreNotEqual(0, 1);
                    Assert.AreNotEqual(0, 1, "Message");
                    Assert.AreNotEqual(message: "Message", notExpected: 0, actual: 1);

                    Assert.AreNotEqual<object>(0, 1);
                    Assert.AreNotEqual<object>(0, 1, "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: 0, actual: 1);

                    // Expected is value type. This is always-failing assert.
                    Assert.AreNotEqual<object>("0", 1);
                    Assert.AreNotEqual<object>("0", 1, "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: "0", actual: 1);

                    // Actual is value type. This is always-failing assert.
                    Assert.AreNotEqual<object>(0, "1");
                    Assert.AreNotEqual<object>(0, "1", "Message");
                    Assert.AreNotEqual<object>(message: "Message", notExpected: 0, actual: "1");

                    // Both are reference types. No diagnostic.
                    Assert.AreNotSame("0", "1");
                    Assert.AreNotSame("0", "1", "Message");
                    Assert.AreNotSame(message: "Message", notExpected: "0", actual: "1");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task AnalyzerMessageShouldBeCorrect()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            using System.Collections.Generic;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    {|#0:Assert.AreSame(0, 0)|};
                    {|#1:Assert.AreNotSame(0, 1)|};
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
                public void TestMethod()
                {
                    Assert.AreEqual(0, 0);
                    Assert.AreNotEqual(0, 1);
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            ExpectedDiagnostics =
            {
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreEqual", "AreSame"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("AreNotEqual", "AreNotSame"),
            },
        }.RunAsync();
    }

    [TestMethod]
    public async Task UseAssertAreSameWithValueTypes_MultiLineWithDifferentIndentation()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreSame(
                        0,
                        0,
                        "values should be the same")|];
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
                    Assert.AreEqual(
                        0,
                        0,
                        "values should be the same");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenValueTypeIsEnum_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    [|Assert.AreSame(DayOfWeek.Monday, DayOfWeek.Tuesday)|];
                    [|Assert.AreNotSame(DayOfWeek.Monday, DayOfWeek.Tuesday)|];
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
                public void TestMethod()
                {
                    Assert.AreEqual(DayOfWeek.Monday, DayOfWeek.Tuesday);
                    Assert.AreNotEqual(DayOfWeek.Monday, DayOfWeek.Tuesday);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenValueTypeIsCustomStruct_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    var a = new MyPoint(1, 2);
                    var b = new MyPoint(3, 4);
                    [|Assert.AreSame(a, b)|];
                    [|Assert.AreNotSame(a, b)|];
                }
            }

            public readonly struct MyPoint(int x, int y)
            {
                public int X { get; } = x;
                public int Y { get; } = y;
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
                    var a = new MyPoint(1, 2);
                    var b = new MyPoint(3, 4);
                    Assert.AreEqual(a, b);
                    Assert.AreNotEqual(a, b);
                }
            }

            public readonly struct MyPoint(int x, int y)
            {
                public int X { get; } = x;
                public int Y { get; } = y;
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenValueTypeIsNullableInt_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    int? a = 1;
                    int? b = 2;
                    [|Assert.AreSame(a, b)|];
                    [|Assert.AreNotSame(a, b)|];
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
                    int? a = 1;
                    int? b = 2;
                    Assert.AreEqual(a, b);
                    Assert.AreNotEqual(a, b);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenBothArgsAreNullLiterals_NoDiagnostic()
    {
        // WalkDownConversion() removes the object conversion, leaving an untyped null literal.
        // The null-propagating IsValueType check therefore does not report a diagnostic.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod()
                {
                    Assert.AreSame((object)null, (object)null);
                    Assert.AreNotSame((object)null, (object)null);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenGenericTypeParameterConstrainedToStruct_Diagnostic()
    {
        // A generic type parameter constrained to 'struct' has IsValueType == true,
        // so the analyzer should report a diagnostic and the fixer should replace the method name.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod<T>() where T : struct
                {
                    T a = default;
                    T b = default;
                    [|Assert.AreSame(a, b)|];
                    [|Assert.AreNotSame(a, b)|];
                }
            }
            """;

        string fixedCode = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod<T>() where T : struct
                {
                    T a = default;
                    T b = default;
                    Assert.AreEqual(a, b);
                    Assert.AreNotEqual(a, b);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenGenericTypeParameterWithNoConstraint_NoDiagnostic()
    {
        // An unconstrained generic type parameter is not known to be a value type at analysis time,
        // so the analyzer should not report a diagnostic even though T can be instantiated with one.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void TestMethod<T>(T a, T b)
                {
                    Assert.AreSame(a, b);
                    Assert.AreNotSame(a, b);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
