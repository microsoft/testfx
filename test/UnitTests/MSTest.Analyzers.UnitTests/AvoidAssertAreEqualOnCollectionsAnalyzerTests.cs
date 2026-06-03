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
    public async Task WhenUsingAssertAreNotEqualWithCollectionExpectedAndNullActual_DoNotReportDiagnostic()
    {
        // Same rationale for Assert.AreNotEqual(x, null): the user is performing a null check, not a
        // collection comparison, so suggesting CollectionAssert.AreNotEqual would be misleading.
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
                    Assert.AreNotEqual(dictionary, null);
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

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 1, "Assert.AreEqual", "List<int>");
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

        await VerifyCodeFixAsync(code, fixedCode, codeActionIndex: 1, "Assert.AreNotEqual", "List<int>");
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

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithExplicitObjectGenericArgumentAndArrayArguments_ReportDiagnostic()
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
                    {|#0:Assert.AreEqual<object>(arr1, arr2)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "int[]"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreNotEqualWithExplicitObjectGenericArgumentAndArrayArguments_ReportDiagnostic()
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
                    {|#0:Assert.AreNotEqual<object>(arr1, arr2)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreNotEqual", "int[]"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithObjectCastOnCollections_ReportDiagnostic()
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
                    {|#0:Assert.AreEqual((object)expected, (object)actual)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithMixedObjectAndCollectionArguments_ReportDiagnostic()
    {
        // When T resolves to `object` because only one argument is widened, we should still flag the call
        // based on the un-converted static type of the other argument.
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
                    object actual = new List<int> { 1, 2 };
                    {|#0:Assert.AreEqual(expected, actual)|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "List<int>"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithExplicitObjectGenericArgumentAndStringArguments_DoNotReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    string s1 = "a";
                    string s2 = "b";
                    Assert.AreEqual<object>(s1, s2);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithObjectArgumentsAndNoCollectionType_DoNotReportDiagnostic()
    {
        // The user explicitly typed both arguments as object with no observable collection type at the call site.
        // Without dataflow analysis we cannot know whether the runtime value is a collection, so we must not fire.
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    object expected = new List<int> { 1, 2 };
                    object actual = new List<int> { 1, 2 };
                    Assert.AreEqual(expected, actual);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnRecord_DoNotReportDiagnostic()
    {
        string code = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    MyRecord r1 = new(1);
                    MyRecord r2 = new(1);
                    Assert.AreEqual(r1, r2);
                }

                private sealed record MyRecord(int Value);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithExplicitObjectGenericArgumentAndNullExpected_DoNotReportDiagnostic()
    {
        // The null-literal short-circuit must keep working regardless of how T is inferred at the call site.
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
                    int[]? actual = [1, 2];
                    Assert.AreEqual<object?>(null, actual);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualWithUserDefinedConversionFromCollectionToNonCollection_DoNotReportDiagnostic()
    {
        // The argument's call-site static type is `Wrapper`, which is not a collection. The fact that
        // a user-defined conversion exists from `int[]` to `Wrapper` must not cause MSTEST0065 to fire —
        // the comparison the user actually wrote is on the Wrapper type, not the underlying array.
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
                    Assert.AreEqual<Wrapper>(arr1, arr2);
                }

                public sealed class Wrapper
                {
                    public static implicit operator Wrapper(int[] values) => new();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
