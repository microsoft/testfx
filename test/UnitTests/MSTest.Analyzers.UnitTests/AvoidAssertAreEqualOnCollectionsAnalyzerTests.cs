// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidAssertAreEqualOnCollectionsAnalyzer,
    MSTest.Analyzers.AvoidAssertAreEqualOnCollectionsFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class AvoidAssertAreEqualOnCollectionsAnalyzerTests
{
    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnList_ReportDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreEqual(new List<int> { 1, 2 }, new List<int> { 1, 2 })|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnArray_ReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    int[] arr1 = [1, 2];
                    int[] arr2 = [1, 2];
                    {|#0:Assert.AreEqual<int[]>(arr1, arr2)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "int[]"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnIEnumerable_ReportDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    IEnumerable<int> seq1 = new List<int> { 1, 2 };
                    IEnumerable<int> seq2 = new List<int> { 1, 2 };
                    {|#0:Assert.AreEqual<IEnumerable<int>>(seq1, seq2)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "IEnumerable<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreNotEqualOnList_ReportDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    {|#0:Assert.AreNotEqual(new List<int> { 1, 2 }, new List<int> { 1, 2 })|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreNotEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnListWithMessage_ReportDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreEqual(expected, actual, $"msg {expected.Count}")|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnListWithComparer_ReportDiagnostic()
    {
        string code = """
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    CollectionComparer comparer = new();
                    {|#0:Assert.AreEqual(expected, actual, comparer)|};
                }

                private sealed class CollectionComparer : IEqualityComparer<List<int>>, IEqualityComparer
                {
                    public bool Equals(List<int> x, List<int> y) => ReferenceEquals(x, y);

                    public int GetHashCode(List<int> obj) => 0;

                    bool IEqualityComparer.Equals(object x, object y) => ReferenceEquals(x, y);

                    int IEqualityComparer.GetHashCode(object obj) => 0;
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnString_DoNotReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual("foo", "foo");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnInt_DoNotReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual(5, 7);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnStringIgnoreCase_DoNotReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual("a", "b", ignoreCase: true);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnNullableListNulls_DoNotReportDiagnostic()
    {
        // When an argument is the null literal, MSTEST0037 (UseProperAssertMethods) already
        // triggers and suggests Assert.IsNull / Assert.IsNotNull, which is the correct fix.
        // MSTEST0065 must stay quiet to avoid a redundant (and misleading) suggestion to switch
        // to CollectionAssert.AreEqual / Assert.AreSequenceEqual.
        string code = """
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Assert.AreEqual<List<int>?>(null, null);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithNullExpectedAndCollectionActual_DoNotReportDiagnostic()
    {
        // Regression test for https://github.com/microsoft/testfx/issues/8655.
        // Assert.AreEqual(null, dictionary) is a null check that MSTEST0037 already catches and
        // converts to Assert.IsNull(dictionary). MSTEST0065 must not report it.
        string code = """
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Dictionary<string, int>? dictionary = null;
                    Assert.AreEqual(null, dictionary);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithCollectionExpectedAndNullActual_DoNotReportDiagnostic()
    {
        // Symmetric variant of the issue above: null is in the actual position.
        string code = """
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Dictionary<string, int>? dictionary = null;
                    Assert.AreEqual(dictionary, null);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreNotEqualWithNullExpectedAndCollectionActual_DoNotReportDiagnostic()
    {
        // Same rationale for Assert.AreNotEqual(null, x): MSTEST0037 converts it to Assert.IsNotNull(x).
        string code = """
            #nullable enable
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    Dictionary<string, int>? dictionary = new();
                    Assert.AreNotEqual(null, dictionary);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CodeFix_Ordered_ForAreEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreSequenceEqual(expected, actual);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 0, "Assert.AreEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_InAnyOrder_ForAreEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreSequenceEqual(expected, actual, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 1, "Assert.AreEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_Equivalent_ForAreEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreEquivalent(expected, actual);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 2, "Assert.AreEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_Ordered_ForAreNotEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreNotEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreNotSequenceEqual(expected, actual);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 0, "Assert.AreNotEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_InAnyOrder_ForAreNotEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreNotEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreNotSequenceEqual(expected, actual, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 1, "Assert.AreNotEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_Equivalent_ForAreNotEqual()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreNotEqual(expected, actual)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreNotEquivalent(expected, actual);
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 2, "Assert.AreNotEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_Message_IsPreserved()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    {|#0:Assert.AreEqual(expected, actual, $"msg {expected.Count}")|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    Assert.AreSequenceEqual(expected, actual, $"msg {expected.Count}");
                }
            }
            """;

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 0, "Assert.AreEqual", "List<int>");
    }

    [TestMethod]
    public async Task CodeFix_WithComparer_IsNotOffered()
    {
        string code = """
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    List<int> expected = [1, 2];
                    List<int> actual = [1, 2];
                    CollectionComparer comparer = new();
                    {|#0:Assert.AreEqual(expected, actual, comparer)|};
                }

                private sealed class CollectionComparer : IEqualityComparer<List<int>>, IEqualityComparer
                {
                    public bool Equals(List<int> x, List<int> y) => ReferenceEquals(x, y);

                    public int GetHashCode(List<int> obj) => 0;

                    bool IEqualityComparer.Equals(object x, object y) => ReferenceEquals(x, y);

                    int IEqualityComparer.GetHashCode(object obj) => 0;
                }
            }
            """;

        await new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = code,
            CodeActionIndex = 0,
            ExpectedDiagnostics = { ExpectedDiagnostic("Assert.AreEqual", "List<int>") },
        }.RunAsync();
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnConstrainedTypeParameter_ReportDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod<T>(T a, T b) where T : IEnumerable<int>
                {
                    {|#0:Assert.AreEqual(a, b)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "T"));
    }

    private static DiagnosticResult ExpectedDiagnostic(string methodName, string typeName)
        => VerifyCS.Diagnostic().WithLocation(0).WithArguments(methodName, typeName);

    private static Task VerifyCodeFixAsync(string code, string fixedCode, int codeActionIndex, string methodName, string typeName)
        => new VerifyCS.Test
        {
            TestCode = code,
            FixedCode = fixedCode,
            CodeActionIndex = codeActionIndex,
            ExpectedDiagnostics = { ExpectedDiagnostic(methodName, typeName) },
        }.RunAsync();
}
