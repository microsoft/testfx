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

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatable_DoNotReportDiagnostic()
    {
        // The type is a collection but declares its own equality via IEquatable<self>, so Assert.AreEqual
        // honors that intentional equality. Suggesting a sequence comparison would second-guess the author (issue #9971).
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreNotEqualOnCollectionImplementingIEquatable_DoNotReportDiagnostic()
    {
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreNotEqual(c1, c2);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionOverridingObjectEquals_DoNotReportDiagnostic()
    {
        // Overriding object.Equals is the same intentional-equality signal as IEquatable<self>.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2);
                }

                private sealed class MyCollection : IEnumerable<int>
                {
                    public override bool Equals(object? obj) => obj is MyCollection;

                    public override int GetHashCode() => 0;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableOfOtherType_ReportDiagnostic()
    {
        // IEquatable<SomethingElse> is not used by EqualityComparer<MyCollection>.Default, so the type still
        // falls back to reference equality — the footgun the rule targets.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    {|#0:Assert.AreEqual(c1, c2)|};
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<string>
                {
                    public bool Equals(string? other) => false;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "MyCollection"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableButWidenedToObject_ReportDiagnostic()
    {
        // The collection declares its own equality via IEquatable<self>, but the call is widened to object.
        // EqualityComparer<object>.Default ignores IEquatable<MyCollection> and uses reference equality, so this
        // is still the footgun the rule targets. The opt-out must key off the selected generic type argument.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    {|#0:Assert.AreEqual<object>(c1, c2)|};
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "MyCollection"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableWithCustomComparer_ReportDiagnostic()
    {
        // A genuinely custom comparer bypasses the type's own IEquatable<self>, so the opt-out must not apply and
        // MSTEST0065 should still fire.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    {|#0:Assert.AreEqual(c1, c2, new CustomComparer())|};
                }

                private sealed class CustomComparer : IEqualityComparer<MyCollection>
                {
                    public bool Equals(MyCollection? x, MyCollection? y) => true;

                    public int GetHashCode(MyCollection obj) => 0;
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "MyCollection"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableWithExplicitDefaultComparer_DoNotReportDiagnostic()
    {
        // `EqualityComparer<MyCollection>.Default` dispatches to the type's own IEquatable<self>, so it is equivalent
        // to the parameterless overload and keeps the opt-out.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2, EqualityComparer<MyCollection>.Default);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionWithBaseTypeDefaultComparer_ReportDiagnostic()
    {
        // IEqualityComparer<in T> is contravariant, so EqualityComparer<Base>.Default compiles as the comparer for a
        // Derived argument. But it dispatches to Base's equality, not Derived's IEquatable<Derived>, so it is a custom
        // comparer for T = Derived and MSTEST0065 must still fire.
        string code = """
            #nullable enable
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
                    Derived c1 = new();
                    Derived c2 = new();
                    {|#0:Assert.AreEqual(c1, c2, EqualityComparer<Base>.Default)|};
                }

                private class Base
                {
                }

                private sealed class Derived : Base, IEnumerable<int>, IEquatable<Derived>
                {
                    public bool Equals(Derived? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "Derived"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableWithNullComparer_DoNotReportDiagnostic()
    {
        // A null comparer is replaced with EqualityComparer<MyCollection>.Default by Assert, so it is equivalent to the
        // parameterless overload and keeps the opt-out.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2, (IEqualityComparer<MyCollection>)null!);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableWithDefaultComparerExpression_DoNotReportDiagnostic()
    {
        // `default(IEqualityComparer<MyCollection>)` constant-folds to null, which Assert replaces with
        // EqualityComparer<MyCollection>.Default, so it keeps the opt-out just like a null literal.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2, default(IEqualityComparer<MyCollection>)!);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnTypeParameterConstrainedToIEquatable_DoNotReportDiagnostic()
    {
        // `where T : IEnumerable<int>, IEquatable<T>` guarantees EqualityComparer<T>.Default honors IEquatable<T>,
        // so the comparison uses the constrained equality, not reference equality.
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod<T>(T a, T b) where T : IEnumerable<int>, IEquatable<T>
                {
                    Assert.AreEqual(a, b);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionOverridingNewVirtualEquals_ReportDiagnostic()
    {
        // The base declares `new virtual bool Equals(object)` (a different slot from object.Equals), and the
        // collection overrides that. EqualityComparer<MyCollection>.Default still dispatches the unchanged
        // object.Equals slot (reference equality), so this must NOT be treated as declaring its own equality.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    {|#0:Assert.AreEqual(c1, c2)|};
                }

                private class Base
                {
                    public new virtual bool Equals(object? obj) => true;
                }

                private sealed class MyCollection : Base, IEnumerable<int>
                {
                    public override bool Equals(object? obj) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "MyCollection"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnStructCollectionWithoutOwnEquality_ReportDiagnostic()
    {
        // A struct implementing IEnumerable<T> inherits ValueType.Equals but declares no equality of its own,
        // so EqualityComparer<T>.Default falls back to ValueType's field-wise (reflection-based) comparison rather
        // than any intentional equality. The walk must stop before System.ValueType so this is still reported.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    MyCollection c1 = default;
                    MyCollection c2 = default;
                    {|#0:Assert.AreEqual(c1, c2)|};
                }

                private struct MyCollection : IEnumerable<int>
                {
                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "MyCollection"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnStructCollectionOverridingObjectEquals_DoNotReportDiagnostic()
    {
        // A struct that explicitly overrides object.Equals is found on the concrete type before ValueType, so its
        // intentional equality is honored and MSTEST0065 is suppressed.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    MyCollection c1 = default;
                    MyCollection c2 = default;
                    Assert.AreEqual(c1, c2);
                }

                private struct MyCollection : IEnumerable<int>
                {
                    public override bool Equals(object? obj) => true;

                    public override int GetHashCode() => 0;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnTypeParameterConstrainedToSelfEquatingType_ReportDiagnostic()
    {
        // `where T : ISelf` with `ISelf : IEquatable<ISelf>` does NOT guarantee T implements IEquatable<T>:
        // a concrete T is only required to be assignable to ISelf. EqualityComparer<T>.Default therefore may still
        // use reference equality, so the diagnostic must be preserved (the constraint's own IEquatable<ISelf> is not T's).
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod<T>(T a, T b) where T : ISelf
                {
                    {|#0:Assert.AreEqual(a, b)|};
                }

                public interface ISelf : IEnumerable<int>, IEquatable<ISelf>
                {
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "T"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnTypeParameterConstrainedToClassOverridingObjectEquals_DoNotReportDiagnostic()
    {
        // A class constraint that overrides object.Equals is inherited by every T, so EqualityComparer<T>.Default
        // uses that intentional equality.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod<T>(T a, T b) where T : BaseCollection
                {
                    Assert.AreEqual(a, b);
                }

                public class BaseCollection : IEnumerable<int>
                {
                    public override bool Equals(object? obj) => true;

                    public override int GetHashCode() => 0;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnTypeParameterWithTransitiveClassConstraintOverridingObjectEquals_DoNotReportDiagnostic()
    {
        // Transitive type-parameter constraint: `where T : U where U : BaseCollection`. Every concrete T derives from
        // BaseCollection and inherits its object.Equals override, so EqualityComparer<T>.Default uses that equality.
        // The constraint traversal must follow the nested type parameter U to reach BaseCollection.
        string code = """
            #nullable enable
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod<T, U>(T a, T b) where T : U where U : BaseCollection
                {
                    Assert.AreEqual(a, b);
                }

                public class BaseCollection : IEnumerable<int>
                {
                    public override bool Equals(object? obj) => true;

                    public override int GetHashCode() => 0;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionImplementingIEquatableOfBaseType_ReportDiagnostic()
    {
        // IEquatable<T> is invariant: EqualityComparer<Derived>.Default requires Derived to implement
        // IEquatable<Derived> exactly. A collection implementing only IEquatable<Base> (and not overriding
        // object.Equals) still falls back to reference equality, so this must be reported.
        string code = """
            #nullable enable
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
                    Derived c1 = new();
                    Derived c2 = new();
                    {|#0:Assert.AreEqual(c1, c2)|};
                }

                private class Base
                {
                }

                private sealed class Derived : Base, IEnumerable<int>, IEquatable<Base>
                {
                    public bool Equals(Base? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreEqual", "Derived"));
    }

    [TestMethod]
    public async Task WhenUsingAssertAreEqualOnCollectionWithExplicitInterfaceIEquatable_DoNotReportDiagnostic()
    {
        // Explicit interface implementation of IEquatable<self> is still the equality EqualityComparer<T>.Default
        // dispatches to (via AllInterfaces), so the opt-out applies.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    Assert.AreEqual(c1, c2);
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    bool IEquatable<MyCollection>.Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenUsingAssertAreNotEqualOnCollectionImplementingIEquatableButWidenedToObject_ReportDiagnostic()
    {
        // AreNotEqual mirror of the widened-to-object case: EqualityComparer<object>.Default ignores
        // IEquatable<MyCollection>, so this is still the reference-equality footgun and must be reported.
        string code = """
            #nullable enable
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
                    MyCollection c1 = new();
                    MyCollection c2 = new();
                    {|#0:Assert.AreNotEqual<object>(c1, c2)|};
                }

                private sealed class MyCollection : IEnumerable<int>, IEquatable<MyCollection>
                {
                    public bool Equals(MyCollection? other) => true;

                    public IEnumerator<int> GetEnumerator() => new List<int>().GetEnumerator();

                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, ExpectedDiagnostic("Assert.AreNotEqual", "MyCollection"));
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
