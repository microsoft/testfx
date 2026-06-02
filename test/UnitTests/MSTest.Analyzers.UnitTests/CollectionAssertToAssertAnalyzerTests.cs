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
    public async Task WhenCollectionAssertAreEquivalentWithIEqualityComparer_NoDiagnostic()
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
                    CollectionAssert.AreEquivalent(a, b, comparer);
                    CollectionAssert.AreEquivalent(a, b, comparer, "message");
                    CollectionAssert.AreNotEquivalent(a, b, comparer);
                    CollectionAssert.AreNotEquivalent(a, b, comparer, "message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_FixesToAreAllOfTypeWithSwappedArgs()
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
                    Assert.AreAllOfType(typeof(int), a);
                    Assert.AreAllOfType(typeof(int), a, "message");
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
    public async Task WhenCollectionAssertIsSubsetOf_NoDiagnostic()
    {
        // IsSubsetOf / IsNotSubsetOf have no direct Assert equivalent today; they are out of scope.
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
                    CollectionAssert.IsSubsetOf(a, b);
                    CollectionAssert.IsNotSubsetOf(b, a);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
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
}
