// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed partial class AssertTelemetryTrackingTests
{
    public void AreAllDistinct_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(items));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(items, comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(nonGenericItems));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(nonGenericItems, nonGenericComparer));
    }

    public void AreAllNotNull_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<string> items = ["a", "b"];
        IEnumerable nonGenericItems = new[] { "a", "b" };
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull(items));
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull(nonGenericItems));
    }

    public void AreAllOfType_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable items = new[] { 1, 2, 3 };
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType(typeof(int), items));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType<int>(items));
    }

    public void AreEquivalent_IsTracked()
    {
        using var assertions = new AssertionScope();
        List<int> expected = [1, 2, 3];
        List<int> actual = [1, 2, 3];
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected, actual));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected, actual, strict: false, "message"));
    }

    public void AreNotEquivalent_IsTracked()
    {
        using var assertions = new AssertionScope();
        List<int> notExpected = [1, 2, 3];
        List<int> actual = [4, 5, 6];
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected, actual));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected, actual, strict: false, "message"));
    }

    public void AreSequenceEqual_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> expected = [1, 2, 3];
        IEnumerable<int> actual = [1, 2, 3];
        IEnumerable nonGenericExpected = new[] { 1, 2, 3 };
        IEnumerable nonGenericActual = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(nonGenericExpected, nonGenericActual));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(nonGenericExpected, nonGenericActual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(nonGenericExpected, nonGenericActual, nonGenericComparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(nonGenericExpected, nonGenericActual, nonGenericComparer, SequenceOrder.InAnyOrder));
    }

    public void AreNotSequenceEqual_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> notExpected = [1, 2, 3];
        IEnumerable<int> actual = [1, 2, 4];
        IEnumerable nonGenericNotExpected = new[] { 1, 2, 3 };
        IEnumerable nonGenericActual = new[] { 1, 2, 4 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(nonGenericNotExpected, nonGenericActual));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(nonGenericNotExpected, nonGenericActual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(nonGenericNotExpected, nonGenericActual, nonGenericComparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(nonGenericNotExpected, nonGenericActual, nonGenericComparer, SequenceOrder.InAnyOrder));
    }

    public void Contains_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        Func<int, bool> predicate = static x => x == 2;
        Func<object?, bool> nonGenericPredicate = static x => x is 2;
        AssertTracked("Assert.Contains", () => Assert.Contains(2, items));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, items, comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, items));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, nonGenericItems));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, nonGenericItems, nonGenericComparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(nonGenericPredicate, nonGenericItems));
        AssertTracked("Assert.Contains", () => Assert.Contains("b", "abc"));
        AssertTracked("Assert.Contains", () => Assert.Contains("b", "abc", StringComparison.Ordinal));
    }

    public void DoesNotContain_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        Func<int, bool> predicate = static x => x == 7;
        Func<object?, bool> nonGenericPredicate = static x => x is 7;
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, items));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, items, comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, items));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, nonGenericItems));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, nonGenericItems, nonGenericComparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(nonGenericPredicate, nonGenericItems));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain("x", "abc"));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain("x", "abc", StringComparison.Ordinal));
    }

    public void ContainsAll_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> expected = [1, 2];
        IEnumerable<int> collection = [1, 2, 3];
        IEnumerable nonGenericExpected = new[] { 1, 2 };
        IEnumerable nonGenericCollection = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected, collection));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected, collection, comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(nonGenericExpected, nonGenericCollection));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(nonGenericExpected, nonGenericCollection, nonGenericComparer));
    }

    public void DoesNotContainAll_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> notExpected = [7, 8];
        IEnumerable<int> collection = [1, 2, 3];
        IEnumerable nonGenericNotExpected = new[] { 7, 8 };
        IEnumerable nonGenericCollection = new[] { 1, 2, 3 };
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        IEqualityComparer nonGenericComparer = EqualityComparer<object>.Default;
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected, collection));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected, collection, comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(nonGenericNotExpected, nonGenericCollection));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(nonGenericNotExpected, nonGenericCollection, nonGenericComparer));
    }

    public void ContainsOnly_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        Func<int, bool> predicate = static x => x > 0;
        Func<object?, bool> nonGenericPredicate = static x => x is int;
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, items));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(nonGenericPredicate, nonGenericItems));
    }

    public void ContainsSingle_IsTracked()
    {
        using var assertions = new AssertionScope();
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        Func<int, bool> predicate = static x => x == 2;
        Func<object?, bool> nonGenericPredicate = static x => x is 2;
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, items));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(nonGenericPredicate, nonGenericItems));
    }

    public void HasCount_IsTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, items));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, items, $"message {suffix}"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, nonGenericItems));
    }

    public void IsEmpty_IsTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        IEnumerable<int> items = [];
        IEnumerable nonGenericItems = Array.Empty<int>();
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(items));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(items, $"message {suffix}"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(nonGenericItems));
    }

    public void IsNotEmpty_IsTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        IEnumerable<int> items = [1, 2, 3];
        IEnumerable nonGenericItems = new[] { 1, 2, 3 };
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(items));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(items, $"message {suffix}"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(nonGenericItems));
    }
}
