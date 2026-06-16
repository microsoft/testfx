// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DuplicateDataRowAnalyzer,
    MSTest.Analyzers.DuplicateDataRowFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class DuplicateDataRowAnalyzerTests
{
    [TestMethod]
    public async Task WhenParameterlessConstructorIsUsed_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow]
                [[|DataRow|]]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenObjectConstructorIsUsed_SameArgument_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(5)]
                [[|DataRow(5)|]]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenObjectConstructorIsUsed_DifferentArgument_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(5)]
                [DataRow(4)]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringArrayConstructorIsUsed_NullArgument_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(null)]
                [[|DataRow(null)|]]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringArrayConstructorIsUsed_SameArray_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(new string[] { "A" })]
                [[|DataRow(new string[] { "A" })|]]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenStringArrayConstructorIsUsed_DifferentArray_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(new string[] { "A" })]
                [DataRow(new string[] { "B" })]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenObjectArrayConstructorIsUsed_SameArray_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, 2)]
                [[|DataRow(1, 2)|]]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenObjectArrayConstructorIsUsed_DifferentArray_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, 2)]
                [DataRow(1, 3)]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenZeroAndNegativeZero_NoDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(0.0d)]
                [DataRow(-0.0d)]
                public static void TestMethod1(double x)
                {
                }

                [TestMethod]
                [DataRow(0.0f)]
                [DataRow(-0.0f)]
                public static void TestMethod2(float x)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenZeroIsDuplicated_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(0.0d)]
                [[|DataRow(0d)|]]
                public static void TestMethod1(double x)
                {
                }

                [TestMethod]
                [DataRow(0.0d)]
                [[|DataRow(0.0d)|]]
                public static void TestMethod2(double x)
                {
                }

                [TestMethod]
                [DataRow(0.0f)]
                [[|DataRow(0f)|]]
                public static void TestMethod3(float x)
                {
                }
            
                [TestMethod]
                [DataRow(0.0f)]
                [[|DataRow(0.0f)|]]
                public static void TestMethod4(float x)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenNegativeZeroIsDuplicated_Diagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(-0.0d)]
                [[|DataRow(-0d)|]]
                public static void TestMethod1(double x)
                {
                }

                [TestMethod]
                [DataRow(-0.0d)]
                [[|DataRow(-0.0d)|]]
                public static void TestMethod2(double x)
                {
                }

                [TestMethod]
                [DataRow(-0.0f)]
                [[|DataRow(-0f)|]]
                public static void TestMethod3(float x)
                {
                }
            
                [TestMethod]
                [DataRow(-0.0f)]
                [[|DataRow(-0.0f)|]]
                public static void TestMethod4(float x)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenDuplicateDataRow_CodeFixRemovesDuplicate()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(5)]
                [[|DataRow(5)|]]
                public static void TestMethod(int x)
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
                [DataRow(5)]
                public static void TestMethod(int x)
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenDuplicateDataRowParameterless_CodeFixRemovesDuplicate()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow]
                [[|DataRow|]]
                public static void TestMethod()
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
                [DataRow]
                public static void TestMethod()
                {
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(code, fixedCode);
    }

    [TestMethod]
    public async Task WhenMultipleDuplicateDataRows_CodeFixRemovesEach()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(1, 2)]
                [[|DataRow(1, 2)|]]
                [[|DataRow(1, 2)|]]
                public static void TestMethod(int x, int y)
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
                [DataRow(1, 2)]
                public static void TestMethod(int x, int y)
                {
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            NumberOfFixAllIterations = 2,
            NumberOfFixAllInDocumentIterations = 2,
            NumberOfFixAllInProjectIterations = 2,
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenEnumArgumentIsDuplicated_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(ConsoleColor.Red)]
                [[|DataRow(ConsoleColor.Red)|]]
                public static void TestMethod(object c)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenEnumArgumentsAreDifferent_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(ConsoleColor.Red)]
                [DataRow(ConsoleColor.Blue)]
                public static void TestMethod(object c)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTypeArgumentIsDuplicated_Diagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(typeof(int))]
                [[|DataRow(typeof(int))|]]
                public static void TestMethod(Type t)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenTypeArgumentsAreDifferent_NoDiagnostic()
    {
        string code = """
            using System;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(typeof(int))]
                [DataRow(typeof(string))]
                public static void TestMethod(Type t)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenArrayContainsNullElement_SameContent_Diagnostic()
    {
        // Tests the IsNull && IsNull path within a nested array element comparison.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(null, 1)]
                [[|DataRow(null, 1)|]]
                public static void TestMethod(object x, int y)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenArrayFirstElementNullDiffersFromNonNull_NoDiagnostic()
    {
        // Tests the asymmetric IsNull || IsNull path: one null element vs one non-null element.
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                [DataRow(null, 1)]
                [DataRow(1, 1)]
                public static void TestMethod(object x, int y)
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }
}
