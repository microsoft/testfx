// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

// Spans and memories cannot be captured by lambdas, so each of them is created inside the lambda from a captured array.
public sealed partial class AssertTelemetryTrackingTests
{
    public void AreAllDistinct_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] array = [1, 2, 3];
        List<int> list = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array, comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct<int, List<int>>(list, comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct((ReadOnlySpan<int>)array));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct((ReadOnlySpan<int>)array, comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array.AsSpan()));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array.AsSpan(), comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct((ReadOnlyMemory<int>)array));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct((ReadOnlyMemory<int>)array, comparer));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array.AsMemory()));
        AssertTracked("Assert.AreAllDistinct", () => Assert.AreAllDistinct(array.AsMemory(), comparer));
    }

    public void AreAllNotNull_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        string[] array = ["a", "b"];
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull(array));
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull((ReadOnlySpan<string>)array));
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull(array.AsSpan()));
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull((ReadOnlyMemory<string>)array));
        AssertTracked("Assert.AreAllNotNull", () => Assert.AreAllNotNull(array.AsMemory()));
    }

    public void AreAllOfType_SpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        object[] objects = [1, 2, 3];
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType(typeof(int), (ReadOnlySpan<object>)objects));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType(typeof(int), objects.AsSpan()));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType(typeof(int), (ReadOnlyMemory<object>)objects));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType(typeof(int), objects.AsMemory()));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType<int, object>((ReadOnlySpan<object>)objects));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType<int, object>(objects.AsSpan()));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType<int, object>((ReadOnlyMemory<object>)objects));
        AssertTracked("Assert.AreAllOfType", () => Assert.AreAllOfType<int, object>(objects.AsMemory()));
    }

    public void AreEquivalent_SpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] expected = [1, 2, 3];
        int[] actual = [1, 2, 3];
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual, strict: true));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected.AsSpan(), actual.AsSpan()));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected.AsSpan(), actual.AsSpan(), strict: true));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual, strict: true));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected.AsMemory(), actual.AsMemory()));
        AssertTracked("Assert.AreEquivalent", () => Assert.AreEquivalent(expected.AsMemory(), actual.AsMemory(), strict: true));
    }

    public void AreNotEquivalent_SpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] notExpected = [1, 2, 3];
        int[] actual = [4, 5, 6];
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual, strict: true));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected.AsSpan(), actual.AsSpan()));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected.AsSpan(), actual.AsSpan(), strict: true));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual, strict: true));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected.AsMemory(), actual.AsMemory()));
        AssertTracked("Assert.AreNotEquivalent", () => Assert.AreNotEquivalent(notExpected.AsMemory(), actual.AsMemory(), strict: true));
    }

    public void AreSequenceEqual_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] expected = [1, 2, 3];
        int[] actual = [1, 2, 3];
        List<int> expectedList = [1, 2, 3];
        List<int> actualList = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actualList));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actualList, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected, actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual<int, List<int>, List<int>>(expectedList, actualList, comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual<int, List<int>, List<int>>(expectedList, actualList, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual, comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan()));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual, comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory()));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer));
        AssertTracked("Assert.AreSequenceEqual", () => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer, SequenceOrder.InAnyOrder));
    }

    public void AreNotSequenceEqual_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] notExpected = [1, 2, 3];
        int[] actual = [4, 5, 6];
        List<int> notExpectedList = [1, 2, 3];
        List<int> actualList = [4, 5, 6];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actualList));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actualList, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected, actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual<int, List<int>, List<int>>(notExpectedList, actualList, comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual<int, List<int>, List<int>>(notExpectedList, actualList, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual, comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan()));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual, comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)actual, comparer, SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory()));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), SequenceOrder.InAnyOrder));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), comparer));
        AssertTracked("Assert.AreNotSequenceEqual", () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), comparer, SequenceOrder.InAnyOrder));
    }

    public void Contains_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] array = [1, 2, 3];
        List<int> list = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        Func<int, bool> predicate = static x => x == 2;
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array, comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, array));
        AssertTracked("Assert.Contains", () => Assert.Contains<int, List<int>>(2, list));
        AssertTracked("Assert.Contains", () => Assert.Contains<int, List<int>>(2, list, comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains<int, List<int>>(predicate, list));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, (ReadOnlySpan<int>)array, comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array.AsSpan()));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array.AsSpan(), comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, array.AsSpan()));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, (ReadOnlyMemory<int>)array, comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array.AsMemory()));
        AssertTracked("Assert.Contains", () => Assert.Contains(2, array.AsMemory(), comparer));
        AssertTracked("Assert.Contains", () => Assert.Contains(predicate, array.AsMemory()));
    }

    public void DoesNotContain_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] array = [1, 2, 3];
        List<int> list = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        Func<int, bool> predicate = static x => x == 7;
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array, comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain<int, List<int>>(7, list));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain<int, List<int>>(7, list, comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain<int, List<int>>(predicate, list));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, (ReadOnlySpan<int>)array, comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array.AsSpan()));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array.AsSpan(), comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, array.AsSpan()));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, (ReadOnlyMemory<int>)array, comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array.AsMemory()));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(7, array.AsMemory(), comparer));
        AssertTracked("Assert.DoesNotContain", () => Assert.DoesNotContain(predicate, array.AsMemory()));
    }

    public void ContainsAll_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] expected = [1, 2];
        int[] collection = [1, 2, 3];
        List<int> expectedList = [1, 2];
        List<int> collectionList = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected, collectionList));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected, collection));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected, collection, comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll<int, List<int>, List<int>>(expectedList, collectionList, comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)collection));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll((ReadOnlySpan<int>)expected, (ReadOnlySpan<int>)collection, comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected.AsSpan(), collection.AsSpan()));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected.AsSpan(), collection.AsSpan(), comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)collection));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll((ReadOnlyMemory<int>)expected, (ReadOnlyMemory<int>)collection, comparer));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected.AsMemory(), collection.AsMemory()));
        AssertTracked("Assert.ContainsAll", () => Assert.ContainsAll(expected.AsMemory(), collection.AsMemory(), comparer));
    }

    public void DoesNotContainAll_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] notExpected = [7, 8];
        int[] collection = [1, 2, 3];
        List<int> notExpectedList = [7, 8];
        List<int> collectionList = [1, 2, 3];
        IEqualityComparer<int> comparer = EqualityComparer<int>.Default;
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected, collectionList));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected, collection));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected, collection, comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll<int, List<int>, List<int>>(notExpectedList, collectionList, comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)collection));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll((ReadOnlySpan<int>)notExpected, (ReadOnlySpan<int>)collection, comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected.AsSpan(), collection.AsSpan()));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected.AsSpan(), collection.AsSpan(), comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)collection));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll((ReadOnlyMemory<int>)notExpected, (ReadOnlyMemory<int>)collection, comparer));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected.AsMemory(), collection.AsMemory()));
        AssertTracked("Assert.DoesNotContainAll", () => Assert.DoesNotContainAll(notExpected.AsMemory(), collection.AsMemory(), comparer));
    }

    public void ContainsOnly_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] array = [1, 2, 3];
        List<int> list = [1, 2, 3];
        Func<int, bool> predicate = static x => x > 0;
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, array));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly<int, List<int>>(predicate, list));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, array.AsSpan()));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.ContainsOnly", () => Assert.ContainsOnly(predicate, array.AsMemory()));
    }

    public void ContainsSingle_PredicateArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int[] array = [1, 2, 3];
        List<int> list = [1, 2, 3];
        Func<int, bool> predicate = static x => x == 2;
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, array));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle<int, List<int>>(predicate, list));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, (ReadOnlySpan<int>)array));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, array.AsSpan()));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, (ReadOnlyMemory<int>)array));
        AssertTracked("Assert.ContainsSingle", () => Assert.ContainsSingle(predicate, array.AsMemory()));
    }

    public void HasCount_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        int[] array = [1, 2, 3];
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array, "message"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array, $"message {suffix}"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, (ReadOnlySpan<int>)array, "message"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, (ReadOnlySpan<int>)array, $"message {suffix}"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array.AsSpan(), "message"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array.AsSpan(), $"message {suffix}"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, (ReadOnlyMemory<int>)array, "message"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, (ReadOnlyMemory<int>)array, $"message {suffix}"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array.AsMemory(), "message"));
        AssertTracked("Assert.HasCount", () => Assert.HasCount(3, array.AsMemory(), $"message {suffix}"));
    }

    public void IsEmpty_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        int[] empty = [];
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty, "message"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty, $"message {suffix}"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty((ReadOnlySpan<int>)empty, "message"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty((ReadOnlySpan<int>)empty, $"message {suffix}"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty.AsSpan(), "message"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty.AsSpan(), $"message {suffix}"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty((ReadOnlyMemory<int>)empty, "message"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty((ReadOnlyMemory<int>)empty, $"message {suffix}"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty.AsMemory(), "message"));
        AssertTracked("Assert.IsEmpty", () => Assert.IsEmpty(empty.AsMemory(), $"message {suffix}"));
    }

    public void IsNotEmpty_ArrayAndSpanOverloads_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        int[] array = [1, 2, 3];
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array, "message"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array, $"message {suffix}"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty((ReadOnlySpan<int>)array, "message"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty((ReadOnlySpan<int>)array, $"message {suffix}"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array.AsSpan(), "message"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array.AsSpan(), $"message {suffix}"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty((ReadOnlyMemory<int>)array, "message"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty((ReadOnlyMemory<int>)array, $"message {suffix}"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array.AsMemory(), "message"));
        AssertTracked("Assert.IsNotEmpty", () => Assert.IsNotEmpty(array.AsMemory(), $"message {suffix}"));
    }
}

#endif
