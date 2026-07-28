// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    private static void ContainsCore<T>(T expected, IEnumerable<T> collection, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        if (collection.Contains(expected) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain);
    }

    private static void ContainsCore<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        if (collection.Contains(expected, comparer) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain, comparer);
    }

    private static void ContainsCore(object? expected, IEnumerable collection, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        CheckParameterNotNull(collection, assertMethodName, "collection");

        if (CollectionContains(collection, expected) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain);
    }

    private static void ContainsCore(object? expected, IEnumerable collection, IEqualityComparer comparer, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        CheckParameterNotNull(collection, assertMethodName, "collection");
        CheckParameterNotNull(comparer, assertMethodName, "comparer");

        if (CollectionContains(collection, expected, comparer) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain, comparer);
    }

    private static void ContainsCore<T>(Func<T, bool> predicate, IEnumerable<T> collection, string assertMethodName, string? message, string predicateExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        if (collection.Any(predicate) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(message, predicateExpression, collectionExpression, shouldContain);
    }

    private static void ContainsCore(Func<object?, bool> predicate, IEnumerable collection, string assertMethodName, string? message, string predicateExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        CheckParameterNotNull(collection, assertMethodName, "collection");
        CheckParameterNotNull(predicate, assertMethodName, "predicate");

        if (collection.Cast<object?>().Any(predicate) == shouldContain)
        {
            return;
        }

        ReportContainsFailure(message, predicateExpression, collectionExpression, shouldContain);
    }

#if NETCOREAPP3_1_OR_GREATER
    private static void ContainsCore<T>(T expected, ReadOnlySpan<T> collection, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);

        bool contains = false;
        foreach (T item in collection)
        {
            if (EqualityComparer<T>.Default.Equals(item, expected))
            {
                contains = true;
                break;
            }
        }

        if (contains == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain);
    }

    private static void ContainsCore<T>(T expected, ReadOnlySpan<T> collection, IEqualityComparer<T> comparer, string assertMethodName, string? message, string expectedExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);

        bool contains = false;
        foreach (T item in collection)
        {
            if (comparer.Equals(item, expected))
            {
                contains = true;
                break;
            }
        }

        if (contains == shouldContain)
        {
            return;
        }

        ReportContainsFailure(expected, message, expectedExpression, collectionExpression, shouldContain, comparer);
    }

    private static void ContainsCore<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string assertMethodName, string? message, string predicateExpression, string collectionExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);

        bool contains = false;
        foreach (T item in collection)
        {
            if (predicate(item))
            {
                contains = true;
                break;
            }
        }

        if (contains == shouldContain)
        {
            return;
        }

        ReportContainsFailure(message, predicateExpression, collectionExpression, shouldContain);
    }
#endif

    private static void ContainsCore(string substring, string value, StringComparison comparisonType, string assertMethodName, string? message, string substringExpression, string valueExpression, bool shouldContain)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        CheckParameterNotNull(value, assertMethodName, "value");
        CheckParameterNotNull(substring, assertMethodName, "substring");

#if NETCOREAPP
        bool contains = value.Contains(substring, comparisonType);
#else
        bool contains = value.IndexOf(substring, comparisonType) >= 0;
#endif
        if (contains == shouldContain)
        {
            return;
        }

        if (shouldContain)
        {
            ReportAssertContainsSubstringFailed(substring, value, comparisonType, message, substringExpression, valueExpression);
        }
        else
        {
            ReportAssertDoesNotContainSubstringFailed(substring, value, comparisonType, message, substringExpression, valueExpression);
        }
    }

    private static void StartsOrEndsWithCore(
        [NotNull] string? expectedAffix,
        [NotNull] string? value,
        StringComparison comparisonType,
        string assertMethodName,
        string expectedParameterName,
        string? message,
        string expectedExpression,
        string valueExpression,
        bool shouldMatch,
        Func<string, string, StringComparison, bool> predicate,
        Action<string, string, StringComparison, string?, string, string> reportFailure)
    {
        TelemetryCollector.TrackAssertionCall(assertMethodName);
        CheckParameterNotNull(value, assertMethodName, "value");
        CheckParameterNotNull(expectedAffix, assertMethodName, expectedParameterName);

        if (predicate(value!, expectedAffix!, comparisonType) == shouldMatch)
        {
            return;
        }

        reportFailure(expectedAffix!, value!, comparisonType, message, expectedExpression, valueExpression);
    }

    private static bool CollectionContains(IEnumerable collection, object? expected, IEqualityComparer? comparer = null)
    {
        foreach (object? item in collection)
        {
            if (comparer is null ? object.Equals(item, expected) : comparer.Equals(item, expected))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportContainsFailure(object? expected, string? message, string expectedExpression, string collectionExpression, bool shouldContain, object? comparer = null)
    {
        if (shouldContain)
        {
            ReportAssertContainsItemFailed(expected, message, expectedExpression, collectionExpression, comparer);
        }
        else
        {
            ReportAssertDoesNotContainItemFailed(expected, message, expectedExpression, collectionExpression, comparer);
        }
    }

    private static void ReportContainsFailure(string? message, string predicateExpression, string collectionExpression, bool shouldContain)
    {
        if (shouldContain)
        {
            ReportAssertContainsPredicateFailed(message, predicateExpression, collectionExpression);
        }
        else
        {
            ReportAssertDoesNotContainPredicateFailed(message, predicateExpression, collectionExpression);
        }
    }
}
