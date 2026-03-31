// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.DuplicateDataRowAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

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
}
