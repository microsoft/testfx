// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using MSTest.Analyzers.CodeFixes;

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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyAnalyzerAsync(code, useMSTestV4: true);
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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
                    CollectionAssert.AreEquivalent<int>(a, b, null);
                    CollectionAssert.AreEquivalent<int>(a, b, null, "message");
                    CollectionAssert.AreNotEquivalent<int>(a, b, null);
                    CollectionAssert.AreNotEquivalent<int>(a, b, null, "message");
                    CollectionAssert.AreEquivalent<int>(a, b, default);
                    CollectionAssert.AreEquivalent<int>(a, b, default, "message");
                    CollectionAssert.AreNotEquivalent<int>(a, b, default);
                    CollectionAssert.AreNotEquivalent<int>(a, b, default, "message");
                    CollectionAssert.AreEquivalent<int>(null, null, null);
                    CollectionAssert.AreNotEquivalent<int>(null, null, null);
                    CollectionAssert.AreEquivalent<int>(null, null, default);
                    CollectionAssert.AreNotEquivalent<int>(null, null, default);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, useMSTestV4: true);
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("AreAllDistinct", "AllItemsAreUnique"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("AreAllDistinct", "AllItemsAreUnique"),
            ],
            fixedCode);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_WithTypeof_NoDiagnostic()
    {
        string code = """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object?> { 1, null, 3 };
                    CollectionAssert.AllItemsAreInstancesOfType(a, typeof(int));
                    CollectionAssert.AllItemsAreInstancesOfType(a, typeof(int), "message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, useMSTestV4: true);
    }

    [TestMethod]
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_WithRuntimeType_NoDiagnostic()
    {
        string code = """
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    var a = new List<object?> { 1, null, 3 };
                    Type t = typeof(int);
                    CollectionAssert.AllItemsAreInstancesOfType(a, t);
                    CollectionAssert.AllItemsAreInstancesOfType(a, t, "message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, useMSTestV4: true);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "TypeofMigrationIsNotOfferedFromStaleDiagnostic")]
    [DataRow(true, DisplayName = "RuntimeTypeMigrationIsNotOfferedFromStaleDiagnostic")]
    public async Task WhenCollectionAssertAllItemsAreInstancesOfType_StaleDiagnostic_NoCodeFix(bool runtimeType)
    {
        string typeExpression = runtimeType ? "expectedType" : "typeof(int)";
        string code = $$"""
            #nullable enable
            using System;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    var collection = new List<object?> { 1, null, 3 };
                    Type expectedType = typeof(int);
                    CollectionAssert.AllItemsAreInstancesOfType(collection, {{typeExpression}}, "message");
                }
            }
            """;

        using var workspace = new AdhocWorkspace();
        Document document = await AnalyzerContinuationTestHelpers.CreateDocumentAsync(workspace, code, useMSTestV4: true);
        AnalyzerContinuationTestHelpers.AssertCompilerErrors((await document.Project.GetCompilationAsync())!);
        SyntaxNode root = (await document.GetSyntaxRootAsync())!;
        InvocationExpressionSyntax invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add("ProperAssertMethodNameKey", "AreAllOfType")
            .Add("FixKindKey", "InstanceOfType");
        var stale = Diagnostic.Create(
            new CollectionAssertToAssertAnalyzer().SupportedDiagnostics[0],
            invocation.GetLocation(),
            properties: properties,
            messageArgs: ["AreAllOfType", "AllItemsAreInstancesOfType"]);
        Assert.IsEmpty(await AnalyzerContinuationTestHelpers.GetActionsAsync(document, stale, new CollectionAssertToAssertFixer()));
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments("DoesNotContain", "DoesNotContain"),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments("DoesNotContain", "DoesNotContain"),
            ],
            fixedCode);
    }

    [TestMethod]
    [DataRow("Contains", false, DisplayName = "ContainsNullWithoutNullableAnnotations")]
    [DataRow("Contains", true, DisplayName = "ContainsNullWithNullableAnnotations")]
    [DataRow("DoesNotContain", false, DisplayName = "DoesNotContainNullWithoutNullableAnnotations")]
    [DataRow("DoesNotContain", true, DisplayName = "DoesNotContainNullWithNullableAnnotations")]
    public async Task WhenCollectionAssertMembershipWithNullElement_CastsToObject(string method, bool nullableAnnotations)
    {
        string nullableDirective = nullableAnnotations ? "enable" : "disable";
        string objectType = nullableAnnotations ? "object?" : "object";
        string code = $$"""
            #nullable {{nullableDirective}}
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    ICollection collection = new {{objectType}}[] { null };
                    {|#0:CollectionAssert.{{method}}(collection, null)|};
                    {|#1:CollectionAssert.{{method}}(collection, default, "context")|};
                    {|#2:CollectionAssert.{{method}}(element: null, message: "named", collection: collection)|};
                    {|#3:CollectionAssert.{{method}}(collection, true ? null : "x")|};
                }
            }
            """;

        string fixedCode = $$"""
            #nullable {{nullableDirective}}
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    ICollection collection = new {{objectType}}[] { null };
                    Assert.{{method}}(({{objectType}})null, collection);
                    Assert.{{method}}(({{objectType}})default, collection, "context");
                    Assert.{{method}}(({{objectType}})null, collection, "named");
                    Assert.{{method}}(({{objectType}})(true ? null : "x"), collection);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments(method, method),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments(method, method),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments(method, method),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(3).WithArguments(method, method),
            ],
            fixedCode);
    }

    [TestMethod]
    [DataRow("IsSubsetOf", "ContainsAll", DisplayName = "SubsetKeepsOperandOrder")]
    [DataRow("IsNotSubsetOf", "DoesNotContainAll", DisplayName = "NotSubsetKeepsOperandOrder")]
    public async Task WhenCollectionAssertIsSubsetOf_FixesToContainsAll(string legacyMethod, string assertMethod)
    {
        string expectedParameter = assertMethod == "ContainsAll" ? "expected" : "notExpected";
        string code = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    ICollection subset = new[] { 1, 1 };
                    ICollection superset = new[] { 1, 1, 2 };
                    {|#0:CollectionAssert.{{legacyMethod}}(subset, superset)|};
                    {|#1:CollectionAssert.{{legacyMethod}}(subset, superset, "message")|};
                    {|#2:CollectionAssert.{{legacyMethod}}(message: "context", superset: superset, subset: subset)|};
                }
            }
            """;

        string fixedCode = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class MyTestClass
            {
                [TestMethod]
                public void MyTestMethod()
                {
                    ICollection subset = new[] { 1, 1 };
                    ICollection superset = new[] { 1, 1, 2 };
                    Assert.{{assertMethod}}({{expectedParameter}}: subset, collection: superset);
                    Assert.{{assertMethod}}({{expectedParameter}}: subset, collection: superset, message: "message");
                    Assert.{{assertMethod}}(message: "context", collection: superset, {{expectedParameter}}: subset);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments(assertMethod, legacyMethod),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments(assertMethod, legacyMethod),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(2).WithArguments(assertMethod, legacyMethod),
            ],
            fixedCode);
    }

    [TestMethod]
    [DataRow("IsSubsetOf", DisplayName = "OlderFrameworkHasNoContainsAll")]
    [DataRow("IsNotSubsetOf", DisplayName = "OlderFrameworkHasNoDoesNotContainAll")]
    public async Task WhenCollectionAssertSubsetReplacementIsUnavailable_NoDiagnostic(string legacyMethod)
    {
        string code = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    ICollection subset = new[] { 1 };
                    ICollection superset = new[] { 1, 2 };
                    CollectionAssert.{{legacyMethod}}(subset, superset);
                    CollectionAssert.{{legacyMethod}}(subset, superset, "message");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, useMSTestV4: true);
    }

    [TestMethod]
    [DataRow("IsSubsetOf", "ContainsAll", DisplayName = "TypedSubsetKeepsObjectEquality")]
    [DataRow("IsNotSubsetOf", "DoesNotContainAll", DisplayName = "TypedNotSubsetKeepsObjectEquality")]
    public async Task WhenCollectionAssertSubsetUsesTypedCollections_PreservesObjectEquality(string legacyMethod, string assertMethod)
    {
        string expectedParameter = assertMethod == "ContainsAll" ? "expected" : "notExpected";
        string code = $$"""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    var subset = new List<Item> { new Item(1) };
                    var superset = new List<Item> { new Item(1) };
                    {|#0:CollectionAssert.{{legacyMethod}}(subset, superset)|};
                }
            }

            public sealed class Item : IEquatable<Item>
            {
                private readonly int _value;
                public Item(int value) => _value = value;
                public bool Equals(Item other) => other._value == _value;
                public override int GetHashCode() => _value;
            }
            """;
        string fixedCode = code
            .Replace(
                "{|#0:CollectionAssert." + legacyMethod + "(subset, superset)|}",
                "Assert." + assertMethod + "(" + expectedParameter + ": (IEnumerable)subset, collection: superset)");

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments(assertMethod, legacyMethod),
            fixedCode);
    }

    [TestMethod]
    [DataRow("IsSubsetOf", "ContainsAll", DisplayName = "SubsetNullMessagesRemainNamed")]
    [DataRow("IsNotSubsetOf", "DoesNotContainAll", DisplayName = "NotSubsetNullMessagesRemainNamed")]
    public async Task WhenCollectionAssertSubsetHasNullMessage_PreservesMessageOverload(string legacyMethod, string assertMethod)
    {
        string expectedParameter = assertMethod == "ContainsAll" ? "expected" : "notExpected";
        string code = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    ICollection subset = new[] { 1 };
                    ICollection superset = new[] { 1, 2 };
                    {|#0:CollectionAssert.{{legacyMethod}}(subset, superset, null)|};
                    {|#1:CollectionAssert.{{legacyMethod}}(subset, superset, default)|};
                }
            }
            """;
        string fixedCode = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    ICollection subset = new[] { 1 };
                    ICollection superset = new[] { 1, 2 };
                    Assert.{{assertMethod}}({{expectedParameter}}: subset, collection: superset, message: null);
                    Assert.{{assertMethod}}({{expectedParameter}}: subset, collection: superset, message: default);
                }
            }
            """;

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            [
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments(assertMethod, legacyMethod),
                VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(1).WithArguments(assertMethod, legacyMethod),
            ],
            fixedCode);
    }

    [TestMethod]
    [DataRow("IsSubsetOf", "ContainsAll", DisplayName = "SubsetKeepsNamedEvaluationOrder")]
    [DataRow("IsNotSubsetOf", "DoesNotContainAll", DisplayName = "NotSubsetKeepsNamedEvaluationOrder")]
    public async Task WhenCollectionAssertSubsetArgumentsHaveSideEffects_PreservesSourceOrder(string legacyMethod, string assertMethod)
    {
        string expectedParameter = assertMethod == "ContainsAll" ? "expected" : "notExpected";
        string code = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    int i = 0;
                    {|#0:CollectionAssert.{{legacyMethod}}(superset: new[] { ++i }, subset: new[] { i })|};
                }
            }
            """;
        string fixedCode = $$"""
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    int i = 0;
                    Assert.{{assertMethod}}(collection: new[] { ++i }, {{expectedParameter}}: (IEnumerable)(new[] { i }));
                }
            }
            """;

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            VerifyCS.DiagnosticIgnoringAdditionalLocations().WithLocation(0).WithArguments(assertMethod, legacyMethod),
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
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

        await VerifyCS.VerifyCodeFixWithLegacyMSTestAsync(
            code,
            VerifyCS.Diagnostic().WithLocation(0).WithArguments("AreSequenceEqual", "AreEqual"),
            fixedCode);
    }
}
