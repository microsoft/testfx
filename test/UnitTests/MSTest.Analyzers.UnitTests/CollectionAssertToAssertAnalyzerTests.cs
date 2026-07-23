// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.CollectionAssertToAssertAnalyzer,
    MSTest.Analyzers.CodeFixes.CollectionAssertToAssertFixer>;

namespace MSTest.Analyzers.Test;

[TestClass]
public sealed class CollectionAssertToAssertAnalyzerTests
{
    [TestMethod]
    public async Task WhenCollectionAssertAreEqual_FixesToAreSequenceEqual()
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.AreEqual(a, b)|};
                    {|#1:CollectionAssert.AreEqual(a, b, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 1, 2, 3 };
                    Assert.AreSequenceEqual(a, b);
                    Assert.AreSequenceEqual(a, b, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreSequenceEqual", "AreEqual"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreSequenceEqual", "AreEqual"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreNotEqual_FixesToAreNotSequenceEqual()
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 3, 2, 1 };
                    {|#0:CollectionAssert.AreNotEqual(a, b)|};
                    {|#1:CollectionAssert.AreNotEqual(a, b, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 3, 2, 1 };
                    Assert.AreNotSequenceEqual(a, b);
                    Assert.AreNotSequenceEqual(a, b, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotSequenceEqual", "AreNotEqual"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreNotSequenceEqual", "AreNotEqual"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreEqualWithIComparer_NoDiagnostic()
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 1, 2, 3 };
                    IComparer comparer = Comparer<int>.Default;
                    CollectionAssert.AreEqual(a, b, comparer);
                    CollectionAssert.AreEqual(a, b, comparer, "message");
                    CollectionAssert.AreNotEqual(a, b, comparer);
                    CollectionAssert.AreNotEqual(a, b, comparer, "message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreEquivalent_FixesToAreSequenceEqualWithInAnyOrder()
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 3, 2, 1 };
                    {|#0:CollectionAssert.AreEquivalent(a, b)|};
                    {|#1:CollectionAssert.AreEquivalent(a, b, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 3, 2, 1 };
                    Assert.AreSequenceEqual(a, b, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                    Assert.AreSequenceEqual(a, b, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreSequenceEqual", "AreEquivalent"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreSequenceEqual", "AreEquivalent"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreNotEquivalent_FixesToAreNotSequenceEqualWithInAnyOrder()
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 4, 5, 6 };
                    {|#0:CollectionAssert.AreNotEquivalent(a, b)|};
                    {|#1:CollectionAssert.AreNotEquivalent(a, b, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 4, 5, 6 };
                    Assert.AreNotSequenceEqual(a, b, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                    Assert.AreNotSequenceEqual(a, b, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotSequenceEqual", "AreNotEquivalent"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreNotSequenceEqual", "AreNotEquivalent"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreEquivalentWithIEqualityComparer_FixesToAreSequenceEqual()
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
                    IEnumerable<int> a = new List<int> { 1, 2, 3 };
                    IEnumerable<int> b = new List<int> { 3, 2, 1 };
                    var comparer = EqualityComparer<int>.Default;
                    {|#0:CollectionAssert.AreEquivalent(a, b, comparer)|};
                    {|#1:CollectionAssert.AreEquivalent(a, b, comparer, "message")|};
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
                    IEnumerable<int> a = new List<int> { 1, 2, 3 };
                    IEnumerable<int> b = new List<int> { 3, 2, 1 };
                    var comparer = EqualityComparer<int>.Default;
                    Assert.AreSequenceEqual(a, b, comparer, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                    Assert.AreSequenceEqual(a, b, comparer, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreSequenceEqual", "AreEquivalent"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreSequenceEqual", "AreEquivalent"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAreNotEquivalentWithIEqualityComparer_FixesToAreNotSequenceEqual()
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
                    IEnumerable<int> a = new List<int> { 1, 2, 3 };
                    IEnumerable<int> b = new List<int> { 3, 2, 1 };
                    var comparer = EqualityComparer<int>.Default;
                    {|#0:CollectionAssert.AreNotEquivalent(a, b, comparer)|};
                    {|#1:CollectionAssert.AreNotEquivalent(a, b, comparer, "message")|};
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
                    IEnumerable<int> a = new List<int> { 1, 2, 3 };
                    IEnumerable<int> b = new List<int> { 3, 2, 1 };
                    var comparer = EqualityComparer<int>.Default;
                    Assert.AreNotSequenceEqual(a, b, comparer, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder);
                    Assert.AreNotSequenceEqual(a, b, comparer, Microsoft.VisualStudio.TestTools.UnitTesting.SequenceOrder.InAnyOrder, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreNotSequenceEqual", "AreNotEquivalent"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreNotSequenceEqual", "AreNotEquivalent"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreNotNull_FixesToAreAllNotNull()
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
                    var a = new List<object> { new(), new() };
                    {|#0:CollectionAssert.AllItemsAreNotNull(a)|};
                    {|#1:CollectionAssert.AllItemsAreNotNull(a, "message")|};
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
                    var a = new List<object> { new(), new() };
                    Assert.AreAllNotNull(a);
                    Assert.AreAllNotNull(a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreAllNotNull", "AllItemsAreNotNull"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreAllNotNull", "AllItemsAreNotNull"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreUnique_FixesToAreAllDistinct()
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
                    var a = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.AllItemsAreUnique(a)|};
                    {|#1:CollectionAssert.AllItemsAreUnique(a, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    Assert.AreAllDistinct(a);
                    Assert.AreAllDistinct(a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreAllDistinct", "AllItemsAreUnique"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreAllDistinct", "AllItemsAreUnique"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_WithTypeof_FixesToGenericAreAllOfType()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object> { 1, 2, 3 };
                    {|#0:CollectionAssert.AllItemsAreInstancesOfType(a, typeof(int))|};
                    {|#1:CollectionAssert.AllItemsAreInstancesOfType(a, typeof(int), "message")|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object> { 1, 2, 3 };
                    Assert.AreAllOfType<int>(a);
                    Assert.AreAllOfType<int>(a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreAllOfType", "AllItemsAreInstancesOfType"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreAllOfType", "AllItemsAreInstancesOfType"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_WithRuntimeType_FixesToNonGenericAreAllOfTypeWithSwappedArgs()
    {
        string code = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object> { 1, 2, 3 };
                    Type t = typeof(int);
                    {|#0:CollectionAssert.AllItemsAreInstancesOfType(a, t)|};
                    {|#1:CollectionAssert.AllItemsAreInstancesOfType(a, t, "message")|};
                }
            }
            """;

        string fixedCode = """
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object> { 1, 2, 3 };
                    Type t = typeof(int);
                    Assert.AreAllOfType(t, a);
                    Assert.AreAllOfType(t, a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreAllOfType", "AllItemsAreInstancesOfType"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreAllOfType", "AllItemsAreInstancesOfType"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertContains_FixesToContainsWithSwappedArgs()
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
                    var a = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.Contains(a, 2)|};
                    {|#1:CollectionAssert.Contains(a, 2, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    Assert.Contains(2, a);
                    Assert.Contains(2, a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "Contains"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("Contains", "Contains"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertDoesNotContain_FixesToDoesNotContainWithSwappedArgs()
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
                    var a = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.DoesNotContain(a, 4)|};
                    {|#1:CollectionAssert.DoesNotContain(a, 4, "message")|};
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
                    var a = new List<int> { 1, 2, 3 };
                    Assert.DoesNotContain(4, a);
                    Assert.DoesNotContain(4, a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "DoesNotContain"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("DoesNotContain", "DoesNotContain"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertIsSubsetOf_FixesToAssertIsSubsetOf()
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
                    var a = new List<int> { 1, 2 };
                    var b = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.IsSubsetOf(a, b)|};
                    {|#1:CollectionAssert.IsSubsetOf(a, b, "message")|};
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
                    var a = new List<int> { 1, 2 };
                    var b = new List<int> { 1, 2, 3 };
                    Assert.IsSubsetOf(a, b);
                    Assert.IsSubsetOf(a, b, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsSubsetOf", "IsSubsetOf"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsSubsetOf", "IsSubsetOf"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertIsNotSubsetOf_FixesToAssertIsNotSubsetOf()
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
                    var a = new List<int> { 1, 2 };
                    var b = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.IsNotSubsetOf(b, a)|};
                    {|#1:CollectionAssert.IsNotSubsetOf(b, a, "message")|};
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
                    var a = new List<int> { 1, 2 };
                    var b = new List<int> { 1, 2, 3 };
                    Assert.IsNotSubsetOf(b, a);
                    Assert.IsNotSubsetOf(b, a, "message");
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("IsNotSubsetOf", "IsNotSubsetOf"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("IsNotSubsetOf", "IsNotSubsetOf"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenNotCollectionAssertMethod_NoDiagnostic()
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
                    var a = new List<int> { 1, 2, 3 };
                    Assert.AreSequenceEqual(a, a);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task WhenCollectionAssertContains_PreservesEmptyLines()
    {
        string code = """
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void ShouldNotRemoveEmptyLine()
                {
                    var a = new List<int> { 1, 2, 3 };

                    {|#0:CollectionAssert.Contains(a, 1)|};

                    {|#1:CollectionAssert.Contains(a, 2)|};
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
                public void ShouldNotRemoveEmptyLine()
                {
                    var a = new List<int> { 1, 2, 3 };

                    Assert.Contains(1, a);

                    Assert.Contains(2, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            [
                VerifyCS.Diagnostic().WithLocation(0).WithArguments("Contains", "Contains"),
                VerifyCS.Diagnostic().WithLocation(1).WithArguments("Contains", "Contains"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertContains_HandlesNamedAndOutOfOrderArguments()
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
                    var a = new List<int> { 1, 2, 3 };
                    {|#0:CollectionAssert.Contains(element: 2, collection: a)|};
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
                    var a = new List<int> { 1, 2, 3 };
                    Assert.Contains(2, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("Contains", "Contains"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertIsFullyQualified_FixPreservesQualifier()
    {
        // Regression: the fixer must preserve a fully-qualified CollectionAssert qualifier so the
        // rewritten call binds to MSTest's Assert even when the file has no
        // `using Microsoft.VisualStudio.TestTools.UnitTesting;` directive.
        string code = """
            using System.Collections.Generic;

            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<int> { 1, 2, 3 };
                    {|#0:Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.Contains(a, 1)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;

            [Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<int> { 1, 2, 3 };
                    Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Contains(1, a);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("Contains", "Contains"),
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertIsGlobalQualified_FixPreservesGlobalQualifier()
    {
        // Regression: `global::` qualifier must be preserved so the rewrite continues to bind unambiguously.
        string code = """
            using System.Collections.Generic;

            [global::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 1, 2, 3 };
                    {|#0:global::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.AreEqual(a, b)|};
                }
            }
            """;

        string fixedCode = """
            using System.Collections.Generic;

            [global::Microsoft.VisualStudio.TestTools.UnitTesting.TestClass]
            public class MyTestClass
            {
                [global::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<int> { 1, 2, 3 };
                    var b = new List<int> { 1, 2, 3 };
                    global::Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreSequenceEqual(a, b);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreSequenceEqual", "AreEqual"),
            fixedCode);
    }
}
