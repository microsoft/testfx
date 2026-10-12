// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

using VerifyCS = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.AvoidEnumerableAssertionsOnQueryableAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyProperAssert = MSTest.Analyzers.Test.CSharpCodeFixVerifier<
    MSTest.Analyzers.UseProperAssertMethodsAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using VerifyVB = MSTest.Analyzers.Test.VisualBasicCodeFixVerifier<
    MSTest.Analyzers.AvoidEnumerableAssertionsOnQueryableAnalyzer,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;

namespace MSTest.Analyzers.UnitTests;

[TestClass]
public sealed class AvoidEnumerableAssertionsOnQueryableAnalyzerTests
{
    private const string CountBearingQueryTypes = """
        using System;
        using System.Linq;
        using System.Collections;
        using System.Collections.Generic;
        using System.Linq.Expressions;
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        public abstract class Query<T> : IQueryable<T>
        {
            public abstract Type ElementType { get; }
            public abstract Expression Expression { get; }
            public abstract IQueryProvider Provider { get; }
            public abstract IEnumerator<T> GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public abstract class GenericCountQuery<T> : Query<T>, ICollection<T>
        {
            public abstract int Count { get; }
            public abstract bool IsReadOnly { get; }
            public abstract void Add(T item);
            public abstract void Clear();
            public abstract bool Contains(T item);
            public abstract void CopyTo(T[] array, int arrayIndex);
            public abstract bool Remove(T item);
        }

        public abstract class NonGenericCountQuery<T> : Query<T>, ICollection
        {
            public abstract int Count { get; }
            public abstract bool IsSynchronized { get; }
            public abstract object SyncRoot { get; }
            public abstract void CopyTo(Array array, int index);
        }

        public abstract class ReadOnlyCountQuery<T> : Query<T>, IReadOnlyCollection<T>
        {
            public abstract int Count { get; }
        }
        """;

    [TestMethod]
    [DataRow("Assert.HasCount(1, query)", "HasCount", "Count", DisplayName = "CSharp HasCount")]
    [DataRow("Assert.IsEmpty(query)", "IsEmpty", "Any", DisplayName = "CSharp IsEmpty")]
    [DataRow("Assert.IsNotEmpty(query)", "IsNotEmpty", "Any", DisplayName = "CSharp IsNotEmpty")]
    [DataRow("Assert.ContainsSingle(query)", "ContainsSingle", "Count", DisplayName = "CSharp ContainsSingle")]
    public async Task QueryableCountAndExistenceAssertions_Diagnostic(string assertion, string method, string queryOperation)
    {
        string code = $$"""
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    {|#0:{{assertion}}|};
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code, VerifyCS.Diagnostic()
            .WithLocation(0)
            .WithSeverity(DiagnosticSeverity.Info)
            .WithMessage($"Use 'Queryable.{queryOperation}' instead of 'Assert.{method}' to avoid enumerating query results for a count or existence check"));
    }

    [TestMethod]
    [DataRow("Assert.HasCount(1, query)", "HasCount", "Count", DisplayName = "VisualBasic HasCount")]
    [DataRow("Assert.IsEmpty(query)", "IsEmpty", "Any", DisplayName = "VisualBasic IsEmpty")]
    [DataRow("Assert.IsNotEmpty(query)", "IsNotEmpty", "Any", DisplayName = "VisualBasic IsNotEmpty")]
    [DataRow("Assert.ContainsSingle(query)", "ContainsSingle", "Count", DisplayName = "VisualBasic ContainsSingle")]
    public async Task VisualBasicQueryableAssertions_Diagnostic(string assertion, string method, string queryOperation)
    {
        string code = $$"""
            Imports System.Linq
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Class Tests
                Public Sub Check(query As IQueryable(Of Integer))
                    {|#0:{{assertion}}|}
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code, VerifyVB.Diagnostic()
            .WithLocation(0)
            .WithArguments($"Assert.{method}", queryOperation));
    }

    [TestMethod]
    public async Task QueryExpressionsAndOverloads_Diagnostic()
    {
        string code = """
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    [|Assert.HasCount(1, query.Where(i => i > 0))|];
                    [|Assert.HasCount(collection: query, expected: 0, message: "empty")|];
                    [|Assert.IsEmpty(collection: query, message: $"{query}")|];
                    [|Assert.IsNotEmpty(query, $"{query}")|];
                    [|Assert.ContainsSingle(query, $"{query}")|];
                    [|Assert.ContainsSingle(i => i > 0, query)|];
                    [|Assert.ContainsSingle(collection: query, predicate: i => i > 0, message: "one")|];
                    [|Assert.HasCount<int>(1, query.OrderBy(i => i))|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task QueryImplementationsAndConstraints_Diagnostic()
    {
        string code = """
            using System.Linq;
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public interface IQuery : IOrderedQueryable<int> { }

            public abstract class Query : IQuery
            {
                public abstract System.Type ElementType { get; }
                public abstract Expression Expression { get; }
                public abstract IQueryProvider Provider { get; }
                public abstract IEnumerator<int> GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            }

            public class Tests
            {
                public void Check(Query query, IQuery derived, IQueryable nonGeneric)
                {
                    [|Assert.HasCount(1, query)|];
                    [|Assert.IsEmpty(derived)|];
                    [|Assert.IsNotEmpty(nonGeneric)|];
                    [|Assert.ContainsSingle(nonGeneric)|];
                }

                public void Check<TQuery, TBase>(TQuery query, TBase other)
                    where TQuery : TBase
                    where TBase : IQueryable<int>
                {
                    [|Assert.IsEmpty(query)|];
                    [|Assert.HasCount(1, other)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task AsEnumerableAndBuiltInCasts_Diagnostic()
    {
        string code = """
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    [|Assert.IsEmpty((IEnumerable<int>)query)|];
                    [|Assert.HasCount(1, (IEnumerable)query)|];
                    [|Assert.IsNotEmpty(((query)))|];
                    [|Assert.IsEmpty(query.AsEnumerable())|];
                    [|Assert.ContainsSingle(Enumerable.AsEnumerable(query))|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task IgnoredContainsSingleResults_Diagnostic()
    {
        string code = """
            using System;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    [|Assert.ContainsSingle(query)|];
                    _ = [|Assert.ContainsSingle(query)|];
                    _ = (object)([|Assert.ContainsSingle(query)|]);
                    _ = [|Assert.ContainsSingle(i => i > 0, query)|];
                    Action check = () => [|Assert.ContainsSingle(query)|];
                }

                public void CheckExpression(IQueryable<int> query) => [|Assert.ContainsSingle(query)|];
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ConsumedContainsSingleResults_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public int Check(IQueryable<string> query)
                {
                    var item = Assert.ContainsSingle(query);
                    item = Assert.ContainsSingle(i => i.Length > 0, query);
                    Assert.AreEqual("item", Assert.ContainsSingle(query));
                    Func<string> getItem = () => Assert.ContainsSingle(query);
                    return Assert.ContainsSingle(query).Length;
                }

                public string GetItem(IQueryable<string> query) => Assert.ContainsSingle(query);
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CompositeContainsSingleDiscardedResults_Diagnostic()
    {
        string code = """
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<string> first, IQueryable<string> second, bool flag, string value)
                {
                    _ = flag ? [|Assert.ContainsSingle(first)|] : [|Assert.ContainsSingle(second)|];
                    _ = ([|Assert.ContainsSingle(first)|], [|Assert.ContainsSingle(second)|]);
                    _ = flag ? ([|Assert.ContainsSingle(first)|], 0) : ([|Assert.ContainsSingle(second)|], 1);
                    _ = ((object)([|Assert.ContainsSingle(first)|]), 0);
                    _ = value ?? [|Assert.ContainsSingle(first)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CompositeContainsSingleConsumedResults_NoDiagnostic()
    {
        string code = """
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<string> first, IQueryable<string> second, IQueryable<bool> conditions, bool flag)
                {
                    var item = flag ? Assert.ContainsSingle(first) : Assert.ContainsSingle(second);
                    var pair = (Assert.ContainsSingle(first), Assert.ContainsSingle(second));
                    Consume(flag ? Assert.ContainsSingle(first) : Assert.ContainsSingle(second));
                    Consume((Assert.ContainsSingle(first), Assert.ContainsSingle(second)));
                    _ = Assert.ContainsSingle(conditions) ? 1 : 0;
                    _ = (Assert.ContainsSingle(conditions) ? 1 : 0, 0);
                    _ = Assert.ContainsSingle(first) ?? "fallback";
                }

                private static void Consume(string item) { }
                private static void Consume((string, string) pair) { }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task UntypedNullCollections_NoDiagnostic()
    {
        string code = """
            using System.Collections;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check()
                {
                    Assert.HasCount<int>(1, null!);
                    Assert.IsEmpty<int>(null!);
                    Assert.IsNotEmpty<int>(null!);
                    Assert.ContainsSingle<int>(null!);
                    Assert.IsEmpty((IEnumerable)null!);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task ScalarChecksAndMaterializedCollections_NoDiagnostic()
    {
        string code = """
            using System.Collections.Generic;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query, IEnumerable<int> enumerable)
                {
                    Assert.AreEqual(1, query.Count(i => i > 0));
                    Assert.AreEqual(0, query.Count());
                    Assert.AreNotEqual(0, query.Count(i => i > 0));
                    Assert.IsTrue(query.Any(i => i > 0));
                    Assert.IsFalse(query.Any());
                    Assert.HasCount(1, query.ToList());
                    Assert.IsEmpty(query.ToArray());
                    Assert.IsNotEmpty(query.ToList());
                    Assert.ContainsSingle(query.ToArray());
                    Assert.HasCount(1, enumerable);
                    Assert.IsEmpty(enumerable);
                    Assert.IsNotEmpty(enumerable);
                    Assert.ContainsSingle(enumerable);
                    Assert.IsEmpty(new int[0]);
                    Assert.IsEmpty("");
                    Assert.Contains(1, query);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task UserDefinedMaterializingConversion_NoDiagnostic()
    {
        string code = """
            using System.Linq;
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public abstract class Query : IQueryable<int>
            {
                public abstract System.Type ElementType { get; }
                public abstract Expression Expression { get; }
                public abstract IQueryProvider Provider { get; }
                public abstract IEnumerator<int> GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                public static implicit operator List<int>(Query query) => query.ToList();
            }

            public class Tests
            {
                public void Check(Query query)
                {
                    Assert.IsEmpty((List<int>)query);
                    Assert.HasCount(1, (List<int>)query);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task UnrelatedAssertMethodsAndQueryableMessage_NoDiagnostic()
    {
        string code = """
            using System;
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public static class OtherAssert
            {
                public static void IsEmpty(IQueryable<int> collection) { }
                public static void HasCount(int expected, IQueryable<int> collection) { }
            }

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    OtherAssert.IsEmpty(query);
                    OtherAssert.HasCount(0, query);
                    Assert.HasCount(0, Array.Empty<int>(), $"{query}");
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VisualBasicQueryPatterns_Diagnostic()
    {
        string code = """
            Imports System.Linq
            Imports System.Collections
            Imports System.Collections.Generic
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Class Tests
                Public Sub Check(query As IQueryable(Of Integer))
                    [|Assert.HasCount(collection:=query.Where(Function(i) i > 0), expected:=1)|]
                    [|Assert.IsEmpty(DirectCast(query, IEnumerable(Of Integer)))|]
                    [|Assert.IsNotEmpty(query.AsEnumerable())|]
                    [|Assert.ContainsSingle(Function(i) i > 0, query)|]
                    Dim item = Assert.ContainsSingle(query)
                    Assert.AreEqual(1, query.Count())
                    Assert.IsTrue(query.Any())
                    Assert.IsEmpty(query.ToList())
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CountBearingQueries_UseCountFastPaths_NoDiagnostic()
    {
        string code = $$"""
            {{CountBearingQueryTypes}}

            public class Tests
            {
                public void Check(GenericCountQuery<string> generic, NonGenericCountQuery<string> nonGeneric)
                {
                    Assert.HasCount(1, generic);
                    Assert.IsEmpty(generic);
                    Assert.IsNotEmpty(generic);
                    Assert.IsEmpty(generic.AsEnumerable());
                    Assert.IsNotEmpty((IEnumerable<string>)generic);
                    Assert.HasCount(1, nonGeneric);
                    Assert.IsEmpty(nonGeneric);
                    Assert.HasCount(1, (IEnumerable)nonGeneric);
                    Assert.IsEmpty((IEnumerable)nonGeneric);
                    Assert.IsNotEmpty((IEnumerable)nonGeneric);
                }

                public void Check<TCollection, TBase>(TCollection query)
                    where TCollection : TBase
                    where TBase : IQueryable<string>, ICollection<string>
                {
                    Assert.HasCount(1, query);
                    Assert.IsEmpty(query);
                    Assert.IsNotEmpty(query);
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task CountBearingQueriesWithoutMatchingFastPath_Diagnostic()
    {
        string code = $$"""
            {{CountBearingQueryTypes}}

            public class Tests
            {
                public void Check(GenericCountQuery<string> generic, NonGenericCountQuery<string> nonGeneric, ReadOnlyCountQuery<string> readOnly)
                {
                    [|Assert.HasCount<object>(1, generic)|];
                    [|Assert.IsEmpty<object>(generic)|];
                    [|Assert.IsNotEmpty<object>(generic)|];
                    [|Assert.HasCount(1, (IEnumerable)generic)|];
                    [|Assert.IsEmpty((IEnumerable)generic)|];
                    [|Assert.IsNotEmpty((IEnumerable)generic)|];
                    [|Assert.ContainsSingle(generic)|];
                    [|Assert.ContainsSingle(nonGeneric)|];
                    [|Assert.ContainsSingle(i => i.Length > 0, generic)|];
                    [|Assert.HasCount(1, readOnly)|];
                    [|Assert.IsEmpty(readOnly)|];
                    [|Assert.IsNotEmpty(readOnly)|];
                }
            }
            """;

        await VerifyCS.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task VisualBasicCountBearingQueries_RespectSelectedOverload()
    {
        string code = """
            Imports System.Linq
            Imports System.Collections
            Imports System.Collections.Generic
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Public Interface ICountedQuery(Of T)
                Inherits IQueryable(Of T), ICollection(Of T)
            End Interface

            Public Class Tests
                Public Sub Check(query As ICountedQuery(Of String))
                    Assert.HasCount(1, query)
                    Assert.IsEmpty(query)
                    Assert.IsNotEmpty(query)
                    Assert.IsEmpty(query.AsEnumerable())
                    [|Assert.ContainsSingle(query)|]
                    [|Assert.HasCount(Of Object)(1, query)|]
                    [|Assert.IsEmpty(DirectCast(query, IEnumerable))|]
                End Sub
            End Class
            """;

        await VerifyVB.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public async Task QueryableScalarChecks_DoNotTriggerMSTEST0037()
    {
        string code = """
            using System.Linq;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                public void Check(IQueryable<int> query)
                {
                    Assert.AreEqual(1, query.Count(i => i > 0));
                    Assert.AreEqual(0, query.Count());
                    Assert.AreNotEqual(0, query.Count(i => i > 0));
                    Assert.IsTrue(query.Any(i => i > 0));
                    Assert.IsFalse(query.Any());
                }
            }
            """;

        await VerifyProperAssert.VerifyAnalyzerAsync(code);
    }

    [TestMethod]
    public void RuleIsAnEnabledPerformanceSuggestion()
    {
        var analyzer = new AvoidEnumerableAssertionsOnQueryableAnalyzer();
        Assert.HasCount(1, analyzer.SupportedDiagnostics);
        DiagnosticDescriptor rule = analyzer.SupportedDiagnostics[0];
        Assert.AreEqual("MSTEST0090", rule.Id);
        Assert.AreEqual("Performance", rule.Category);
        Assert.AreEqual(DiagnosticSeverity.Info, rule.DefaultSeverity);
        Assert.IsTrue(rule.IsEnabledByDefault);
    }
}
