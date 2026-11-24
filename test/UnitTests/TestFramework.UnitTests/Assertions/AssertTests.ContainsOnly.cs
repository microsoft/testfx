// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

public partial class AssertTests : TestContainer
{
    public void ContainsOnly_EmptyCollections_DoesNotEvaluatePredicate()
        => VerifyEmptyPredicateCollections(containsOnly: true);

    public void ContainsOnly_AllMatchingItems_EvaluatesEachItemOnce()
        => VerifySuccessfulPredicateVisits(containsOnly: true);

    public void ContainsOnly_FirstMismatch_ReportsIndexValueAndExpressions()
        => VerifyFirstPredicateCounterexample(containsOnly: true);

    public void ContainsOnly_LaterMismatch_StopsAtFirstCounterexample()
        => VerifyLaterPredicateCounterexample(containsOnly: true);

    public void ContainsOnly_NullElement_IsPassedToPredicateAndReported()
        => VerifyNullPredicateItems(containsOnly: true);

    public void ContainsOnly_SingleUseEnumerable_EnumeratesOnceAndDisposes()
        => VerifySingleUsePredicateEnumerable(containsOnly: true);

    public void ContainsOnly_CounterexampleValue_IsCapturedBeforeEnumeratorDisposal()
        => VerifyMutablePredicateCounterexample(containsOnly: true);

    public void ContainsOnly_NestedEnumerableItem_IsNotEnumeratedForDiagnostics()
        => VerifyNestedPredicateCounterexample(containsOnly: true);

    public void ContainsOnly_CustomValueFormatters_ControlCounterexampleRendering()
        => VerifyCustomPredicateCounterexampleFormatters(containsOnly: true);

    public void ContainsOnly_PredicateException_PropagatesOriginalInstanceAndDisposes()
        => VerifyPredicateExceptionPropagation(containsOnly: true);

    public void ContainsOnly_IteratorExceptions_PropagateOriginalInstance()
        => VerifyPredicateIteratorExceptions(containsOnly: true);

    public void ContainsOnly_CancellationExceptions_PropagateUnchanged()
        => VerifyPredicateCancellationPropagation(containsOnly: true);

    public void ContainsOnly_NullCollections_ReportAssertionParameterFailure()
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes().Take(4))
        {
            foreach (bool nullPredicate in new[] { false, true })
            {
                int visits = 0;
                Func<int, bool>? predicate = nullPredicate ? null : value =>
                {
                    visits++;
                    return value > 0;
                };
                Action action = () => InvokePredicateCollection<int>(true, shape, predicate, null, "ignored context");

                AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
                failure.Message.Should().Be(PredicateParameterMessage("ContainsOnly", "collection"));
                visits.Should().Be(0);
            }
        }
    }

    public void ContainsOnly_NullPredicates_ReportAssertionParameterFailureForEmptyAndNonEmptyInputs()
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            foreach (int[] values in new int[][] { [], [1] })
            {
                Action action = () => InvokePredicateCollection<int>(true, shape, null, values);

                AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
                failure.Message.Should().Be(PredicateParameterMessage("ContainsOnly", "predicate"));
            }
        }

        foreach (bool nonGeneric in new[] { false, true })
        {
            var collection = new PredicateTrackingEnumerable<int>([1]);
            Action action = () => InvokePredicateEnumerable<int>(true, nonGeneric, null, collection);

            action.Should().ThrowExactly<AssertFailedException>().Which.Message
                .Should().Be(PredicateParameterMessage("ContainsOnly", "predicate"));
            collection.GetEnumeratorCalls.Should().Be(0);
            collection.DisposeCalls.Should().Be(0);
        }
    }

    public void ContainsOnly_InvalidParameters_ScopedFailuresStopSafely()
    {
        foreach (bool nonGeneric in new[] { false, true })
        {
            foreach ((bool nullCollection, bool nullPredicate) in new[] { (true, false), (false, true), (true, true) })
            {
                int continued = 0;
                int visits = 0;
                PredicateTrackingEnumerable<int>? collection = nullCollection ? null : new([1]);
                Func<int, bool>? predicate = nullPredicate ? null : value =>
                {
                    visits++;
                    return value > 0;
                };
                Action action = () =>
                {
                    using (Assert.Scope())
                    {
                        InvokePredicateEnumerable(true, nonGeneric, predicate, collection);
                        continued++;
                        Assert.AreEqual(1, 1);
                    }
                };

                AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
                failure.Message.Should().Be(PredicateParameterMessage("ContainsOnly", nullCollection ? "collection" : "predicate"));

                continued.Should().Be(0);
                visits.Should().Be(0);
                if (collection is not null)
                {
                    collection.GetEnumeratorCalls.Should().Be(0);
                    collection.DisposeCalls.Should().Be(0);
                }
            }
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    public void ContainsOnly_SpanMemorySlices_ReportRelativeIndexAndExcludeOutsideItems()
        => VerifyPredicateSlices(containsOnly: true);
#endif

    private static IEnumerable<PredicateCollectionShape> GetPredicateCollectionShapes()
    {
        yield return PredicateCollectionShape.GenericEnumerable;
        yield return PredicateCollectionShape.NonGenericEnumerable;
        yield return PredicateCollectionShape.Array;
        yield return PredicateCollectionShape.Collection;
#if NETCOREAPP3_1_OR_GREATER
        yield return PredicateCollectionShape.ReadOnlySpan;
        yield return PredicateCollectionShape.Span;
        yield return PredicateCollectionShape.ReadOnlyMemory;
        yield return PredicateCollectionShape.Memory;
#endif
    }

    // Each branch calls its public overload directly, with natural caller expressions. Ref structs
    // stay inside this invocation rather than being captured by the Action used to inspect failures.
    private static void InvokePredicateCollection<T>(bool containsOnly, PredicateCollectionShape shape, Func<T, bool>? condition, T[]? values, string? message = null)
    {
        switch (shape)
        {
            case PredicateCollectionShape.GenericEnumerable:
                {
                    Func<T, bool> predicate = condition!;
                    IEnumerable<T> collection = values!;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.NonGenericEnumerable:
                {
                    Func<object?, bool> predicate = condition is null ? null! : value => condition((T)value!);
                    IEnumerable collection = values is null ? null! : new ArrayList(values);
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Array:
                {
                    Func<T, bool> predicate = condition!;
                    T[] collection = values!;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly<T>(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain<T>(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Collection:
                {
                    Func<T, bool> predicate = condition!;
                    List<T> collection = values is null ? null! : new(values);
                    if (containsOnly)
                    {
#if NETCOREAPP3_1_OR_GREATER
                        Assert.ContainsOnly<T, List<T>>(predicate, collection, message);
#else
                        Assert.ContainsOnly(predicate, collection, message);
#endif
                    }
                    else
                    {
#if NETCOREAPP3_1_OR_GREATER
                        Assert.DoesNotContain<T, List<T>>(predicate, collection, message);
#else
                        Assert.DoesNotContain(predicate, collection, message);
#endif
                    }

                    break;
                }

#if NETCOREAPP3_1_OR_GREATER
            case PredicateCollectionShape.ReadOnlySpan:
                {
                    Func<T, bool> predicate = condition!;
                    ReadOnlySpan<T> collection = values;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Span:
                {
                    Func<T, bool> predicate = condition!;
                    Span<T> collection = values;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.ReadOnlyMemory:
                {
                    Func<T, bool> predicate = condition!;
                    ReadOnlyMemory<T> collection = values;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Memory:
                {
                    Func<T, bool> predicate = condition!;
                    Memory<T> collection = values;
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }
#endif

            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }
    }

    private static void InvokePredicateEnumerable<T>(bool containsOnly, bool nonGeneric, Func<T, bool>? condition, IEnumerable<T>? values, string? message = null)
    {
        if (nonGeneric)
        {
            Func<object?, bool> predicate = condition is null ? null! : value => condition((T)value!);
            IEnumerable collection = values!;
            if (containsOnly)
            {
                Assert.ContainsOnly(predicate, collection, message);
            }
            else
            {
                Assert.DoesNotContain(predicate, collection, message);
            }
        }
        else
        {
            Func<T, bool> predicate = condition!;
            IEnumerable<T> collection = values!;
            if (containsOnly)
            {
                Assert.ContainsOnly(predicate, collection, message);
            }
            else
            {
                Assert.DoesNotContain(predicate, collection, message);
            }
        }
    }

    private static string PredicateCounterexampleMessage(bool containsOnly, int index, string item, string? message = null)
    {
        string summary = containsOnly
            ? "Expected collection to contain only elements matching the predicate."
            : "Expected collection to not contain an element matching the predicate.";
        string method = containsOnly ? "ContainsOnly" : "DoesNotContain";
        return string.IsNullOrEmpty(message)
            ? $"""
                Assertion failed. {summary}

                index: {index}
                item:  {item}

                Assert.{method}(predicate, collection)
                """
            : $"""
                Assertion failed. {summary}
                {message}

                index: {index}
                item:  {item}

                Assert.{method}(predicate, collection)
                """;
    }

    private static string PredicateParameterMessage(string method, string parameter)
        => $"Assert.{method} failed. The parameter '{parameter}' is invalid. The value cannot be null.";

    private static void VerifyPredicateFailure(Action action, string expected, bool containsOnly)
    {
        AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
        failure.ExpectedText.Should().Be(containsOnly ? "true" : "false");
        failure.ActualText.Should().Be(containsOnly ? "false" : "true");
    }

    private static void VerifyEmptyPredicateCollections(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            int visits = 0;
            Func<int, bool> predicate = _ =>
            {
                visits++;
                throw new InvalidOperationException("An empty collection must not evaluate its predicate.");
            };
            Action action = () => InvokePredicateCollection<int>(containsOnly, shape, predicate, []);

            action.Should().NotThrow();
            visits.Should().Be(0);
        }
    }

    private static void VerifySuccessfulPredicateVisits(bool containsOnly)
    {
        int[][] inputs = containsOnly ? [[1], [1, 2, 3]] : [[1], [1, 3, 5]];
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            foreach (int[] values in inputs)
            {
                List<int> visits = [];
                Func<int, bool> predicate = value =>
                {
                    visits.Add(value);
                    return containsOnly ? value > 0 : value % 2 == 0;
                };
                Action action = () => InvokePredicateCollection(containsOnly, shape, predicate, values);

                action.Should().NotThrow();
                visits.Should().Equal(values);
            }
        }
    }

    private static void VerifyFirstPredicateCounterexample(bool containsOnly)
    {
        int[] values = containsOnly ? [-1, 2, -3] : [2, 4, 6];
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            List<int> visits = [];
            Func<int, bool> predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value > 0 : value % 2 == 0;
            };
            Action action = () => InvokePredicateCollection(containsOnly, shape, predicate, values, "predicate context");

            VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 0, containsOnly ? "-1" : "2", "predicate context"), containsOnly);
            visits.Should().Equal(containsOnly ? new[] { -1 } : new[] { 2 });
        }
    }

    private static void VerifyLaterPredicateCounterexample(bool containsOnly)
    {
        int[] values = containsOnly ? [1, 2, -3, -4] : [1, 3, 4, 6];
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            List<int> visits = [];
            Func<int, bool> predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value > 0 : value % 2 == 0;
            };
            Action action = () => InvokePredicateCollection(containsOnly, shape, predicate, values);

            VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 2, containsOnly ? "-3" : "4"), containsOnly);
            visits.Should().Equal(containsOnly ? new[] { 1, 2, -3 } : new[] { 1, 3, 4 });
        }
    }

    private static void VerifyNullPredicateItems(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            string?[] values = ["ok", null, "after"];
            List<string?> visits = [];
            Func<string?, bool> predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value is null || value.Length > 0 : value == "missing";
            };
            Action accepted = () => InvokePredicateCollection(containsOnly, shape, predicate, values);

            accepted.Should().NotThrow();
            visits.Should().Equal("ok", null, "after");

            visits.Clear();
            predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value is not null : value is null;
            };
            Action rejected = () => InvokePredicateCollection(containsOnly, shape, predicate, values);

            VerifyPredicateFailure(rejected, PredicateCounterexampleMessage(containsOnly, 1, "null"), containsOnly);
            visits.Should().Equal("ok", null);

            // A string is IEnumerable<char>, but must retain normal quoted/escaped rendering.
            values = ["ok", "bad \"value\"", "after"];
            visits.Clear();
            predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value != "bad \"value\"" : value == "bad \"value\"";
            };
            VerifyPredicateFailure(rejected, PredicateCounterexampleMessage(containsOnly, 1, "\"bad \\\"value\\\"\""), containsOnly);
            visits.Should().Equal("ok", "bad \"value\"");
        }
    }

    private static void VerifySingleUsePredicateEnumerable(bool containsOnly)
    {
        foreach (bool nonGeneric in new[] { false, true })
        {
            foreach (bool fails in new[] { false, true })
            {
                int[] values = containsOnly
                    ? fails ? [1, 2, -3, -4] : [1, 2, 3]
                    : fails ? [1, 3, 4, 6] : [1, 3, 5];
                var collection = new PredicateTrackingEnumerable<int>(values);
                List<int> visits = [];
                Func<int, bool> predicate = value =>
                {
                    visits.Add(value);
                    return containsOnly ? value > 0 : value % 2 == 0;
                };
                Action action = () => InvokePredicateEnumerable(containsOnly, nonGeneric, predicate, collection);

                if (fails)
                {
                    VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 2, containsOnly ? "-3" : "4"), containsOnly);
                    visits.Should().Equal(containsOnly ? new[] { 1, 2, -3 } : new[] { 1, 3, 4 });
                }
                else
                {
                    action.Should().NotThrow();
                    visits.Should().Equal(values);
                }

                collection.GetEnumeratorCalls.Should().Be(1);
                collection.DisposeCalls.Should().Be(1);
            }

            var empty = new PredicateTrackingEnumerable<int>([]);
            int emptyVisits = 0;
            Action emptyAction = () => InvokePredicateEnumerable(containsOnly, nonGeneric, _ =>
            {
                emptyVisits++;
                throw new InvalidOperationException("Empty source predicate.");
            }, empty);

            emptyAction.Should().NotThrow();
            emptyVisits.Should().Be(0);
            empty.GetEnumeratorCalls.Should().Be(1);
            empty.DisposeCalls.Should().Be(1);
        }
    }

    private static void VerifyMutablePredicateCounterexample(bool containsOnly)
    {
        foreach (bool nonGeneric in new[] { false, true })
        {
            var item = new MutablePredicateItem { Value = 17 };
            var collection = new PredicateTrackingEnumerable<MutablePredicateItem>([item], onDispose: () => item.Value = 99);
            int visits = 0;
            Action action = () => InvokePredicateEnumerable(containsOnly, nonGeneric, value =>
            {
                visits++;
                return containsOnly ? value.Value < 0 : value.Value == 17;
            }, collection);

            VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 0, "17"), containsOnly);
            visits.Should().Be(1);
            item.Value.Should().Be(99);
            collection.GetEnumeratorCalls.Should().Be(1);
            collection.DisposeCalls.Should().Be(1);
        }
    }

    private static void VerifyNestedPredicateCounterexample(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            var generic = new PredicateLazyEnumerable();
            var nonGeneric = new PredicateLazyNonGenericEnumerable();
            foreach (object item in new object[] { generic, nonGeneric })
            {
                Action action = () => InvokePredicateCollection<object>(containsOnly, shape, _ => !containsOnly, [item]);
                string expectedItem = $"<{item.GetType().FullName}> (contents not enumerated)";

                VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 0, expectedItem), containsOnly);
            }

            generic.GetEnumeratorCalls.Should().Be(0);
            nonGeneric.GetEnumeratorCalls.Should().Be(0);
        }
    }

    private static void VerifyCustomPredicateCounterexampleFormatters(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            foreach (int mode in new[] { 0, 1, 2, 3, 4 })
            {
                var item = new PredicateLazyEnumerable();
                using IDisposable formatter = mode switch
                {
                    0 => Assert.AddValueFormatter<PredicateLazyEnumerable>(_ => "lazy-item:17"),
                    1 => Assert.AddValueFormatter(next => value => value is PredicateLazyEnumerable ? "lazy-item:17" : next(value)),
                    2 => Assert.AddValueFormatter<PredicateLazyEnumerable>(_ => null),
                    3 => Assert.AddValueFormatter(next => value => next(value)),
                    _ => Assert.AddValueFormatter<PredicateLazyEnumerable>(_ => throw new InvalidOperationException("formatter sentinel")),
                };
                Action action = () => InvokePredicateCollection<PredicateLazyEnumerable>(containsOnly, shape, _ => !containsOnly, [item]);
                string expectedItem = mode < 2 ? "lazy-item:17" : $"<{typeof(PredicateLazyEnumerable).FullName}> (contents not enumerated)";
                if (mode == 4)
                {
                    expectedItem += " (value formatter threw InvalidOperationException)";
                }

                VerifyPredicateFailure(action, PredicateCounterexampleMessage(containsOnly, 0, expectedItem), containsOnly);
                item.GetEnumeratorCalls.Should().Be(0);
            }
        }
    }

    private static void VerifyPredicateExceptionPropagation(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            var sentinel = new InvalidOperationException("predicate sentinel");
            List<int> visits = [];
            Func<int, bool> predicate = value =>
            {
                visits.Add(value);
                return value == 2 ? throw sentinel : containsOnly;
            };
            Action action = () => InvokePredicateCollection(containsOnly, shape, predicate, [1, 2, 3]);

            action.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
            visits.Should().Equal(1, 2);
        }

        foreach (bool nonGeneric in new[] { false, true })
        {
            var sentinel = new InvalidOperationException("predicate cleanup sentinel");
            var collection = new PredicateTrackingEnumerable<int>([1, 2, 3]);
            List<int> visits = [];
            Action action = () => InvokePredicateEnumerable(containsOnly, nonGeneric, value =>
            {
                visits.Add(value);
                return value == 2 ? throw sentinel : containsOnly;
            }, collection);

            action.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
            visits.Should().Equal(1, 2);
            collection.GetEnumeratorCalls.Should().Be(1);
            collection.DisposeCalls.Should().Be(1);
        }
    }

    private static void VerifyPredicateIteratorExceptions(bool containsOnly)
    {
        foreach (bool nonGeneric in new[] { false, true })
        {
            foreach (PredicateIteratorFault stage in new[] { PredicateIteratorFault.GetEnumerator, PredicateIteratorFault.MoveNext, PredicateIteratorFault.Current })
            {
                var sentinel = new InvalidOperationException("iterator sentinel");
                var collection = new PredicateTrackingEnumerable<int>([1], stage, sentinel);
                int visits = 0;
                Action action = () => InvokePredicateEnumerable(containsOnly, nonGeneric, _ =>
                {
                    visits++;
                    return containsOnly;
                }, collection);

                action.Should().ThrowExactly<InvalidOperationException>().Which.Should().BeSameAs(sentinel);
                visits.Should().Be(0);
                collection.GetEnumeratorCalls.Should().Be(1);
                collection.DisposeCalls.Should().Be(stage == PredicateIteratorFault.GetEnumerator ? 0 : 1);
            }
        }
    }

    private static void VerifyPredicateCancellationPropagation(bool containsOnly)
    {
        foreach (bool nonGeneric in new[] { false, true })
        {
            foreach (bool scoped in new[] { false, true })
            {
                foreach (bool iteratorThrows in new[] { false, true })
                {
                    var token = new CancellationToken(canceled: true);
                    var sentinel = new OperationCanceledException("cancellation sentinel", token);
                    var collection = new PredicateTrackingEnumerable<int>(
                        [1],
                        iteratorThrows ? PredicateIteratorFault.MoveNext : PredicateIteratorFault.None,
                        sentinel);
                    int visits = 0;
                    Action invoke = () => InvokePredicateEnumerable(containsOnly, nonGeneric, _ =>
                    {
                        visits++;
                        throw sentinel;
                    }, collection);
                    Action action = () =>
                    {
                        if (scoped)
                        {
                            using (Assert.Scope())
                            {
                                invoke();
                            }
                        }
                        else
                        {
                            invoke();
                        }
                    };

                    OperationCanceledException failure = action.Should().ThrowExactly<OperationCanceledException>().Which;
                    failure.Should().BeSameAs(sentinel);
                    failure.CancellationToken.Should().Be(token);
                    visits.Should().Be(iteratorThrows ? 0 : 1);
                    collection.GetEnumeratorCalls.Should().Be(1);
                    collection.DisposeCalls.Should().Be(1);
                }
            }
        }

#if NETCOREAPP3_1_OR_GREATER
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes().Skip(4))
        {
            foreach (bool scoped in new[] { false, true })
            {
                var token = new CancellationToken(canceled: true);
                var sentinel = new OperationCanceledException("memory cancellation sentinel", token);
                Action action = () =>
                {
                    if (scoped)
                    {
                        using (Assert.Scope())
                        {
                            InvokePredicateCollection<int>(containsOnly, shape, _ => throw sentinel, [1]);
                        }
                    }
                    else
                    {
                        InvokePredicateCollection<int>(containsOnly, shape, _ => throw sentinel, [1]);
                    }
                };

                OperationCanceledException failure = action.Should().ThrowExactly<OperationCanceledException>().Which;
                failure.Should().BeSameAs(sentinel);
                failure.CancellationToken.Should().Be(token);
            }
        }
#endif
    }

#if NETCOREAPP3_1_OR_GREATER
    private static void VerifyPredicateSlices(bool containsOnly)
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes().Skip(4))
        {
            int[] values = containsOnly ? [-50, 1, 0, 2, -90] : [2, 1, 4, 6, 8];
            List<int> visits = [];
            Func<int, bool> predicate = value =>
            {
                visits.Add(value);
                return containsOnly ? value > 0 : value % 2 == 0;
            };
            Action rejected = () => InvokePredicateSlice(containsOnly, shape, predicate, values, 1, 3, "slice context");

            VerifyPredicateFailure(rejected, PredicateCounterexampleMessage(containsOnly, 1, containsOnly ? "0" : "4", "slice context"), containsOnly);
            visits.Should().Equal(containsOnly ? new[] { 1, 0 } : new[] { 1, 4 });

            values = containsOnly ? [-50, 1, 2, 3, -90] : [2, 1, 3, 5, 8];
            visits.Clear();
            Action accepted = () => InvokePredicateSlice(containsOnly, shape, predicate, values, 1, 3);
            accepted.Should().NotThrow();
            visits.Should().Equal(containsOnly ? new[] { 1, 2, 3 } : new[] { 1, 3, 5 });

            visits.Clear();
            predicate = value =>
            {
                visits.Add(value);
                throw new InvalidOperationException("An empty slice must not evaluate its predicate.");
            };
            Action empty = () => InvokePredicateSlice(containsOnly, shape, predicate, values, 1, 0);
            empty.Should().NotThrow();
            visits.Should().BeEmpty();
        }
    }

    private static void InvokePredicateSlice(bool containsOnly, PredicateCollectionShape shape, Func<int, bool> predicate, int[] values, int start, int length, string? message = null)
    {
        switch (shape)
        {
            case PredicateCollectionShape.ReadOnlySpan:
                {
                    ReadOnlySpan<int> collection = values.AsSpan(start, length);
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Span:
                {
                    Span<int> collection = values.AsSpan(start, length);
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.ReadOnlyMemory:
                {
                    ReadOnlyMemory<int> collection = values.AsMemory(start, length);
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            case PredicateCollectionShape.Memory:
                {
                    Memory<int> collection = values.AsMemory(start, length);
                    if (containsOnly)
                    {
                        Assert.ContainsOnly(predicate, collection, message);
                    }
                    else
                    {
                        Assert.DoesNotContain(predicate, collection, message);
                    }

                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }
    }
#endif

    private enum PredicateCollectionShape
    {
        GenericEnumerable,
        NonGenericEnumerable,
        Array,
        Collection,
        ReadOnlySpan,
        Span,
        ReadOnlyMemory,
        Memory,
    }

    private enum PredicateIteratorFault
    {
        None,
        GetEnumerator,
        MoveNext,
        Current,
    }

    private sealed class MutablePredicateItem
    {
        internal int Value { get; set; }

        public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class PredicateLazyEnumerable : IEnumerable<int>
    {
        internal int GetEnumeratorCalls { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            GetEnumeratorCalls++;
            throw new InvalidOperationException("Nested generic enumerable must not be visited by built-in diagnostics.");
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PredicateLazyNonGenericEnumerable : IEnumerable
    {
        internal int GetEnumeratorCalls { get; private set; }

        public IEnumerator GetEnumerator()
        {
            GetEnumeratorCalls++;
            throw new InvalidOperationException("Nested nongeneric enumerable must not be visited by built-in diagnostics.");
        }
    }

    private sealed class PredicateTrackingEnumerable<T>(T[] values, PredicateIteratorFault fault = PredicateIteratorFault.None, Exception? exception = null, Action? onDispose = null) : IEnumerable<T>
    {
        private readonly T[] _values = values;
        private readonly PredicateIteratorFault _fault = fault;
        private readonly Exception? _exception = exception;
        private readonly Action? _onDispose = onDispose;

        internal int GetEnumeratorCalls { get; private set; }

        internal int DisposeCalls { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return GetEnumeratorCalls != 1
                ? throw new InvalidOperationException("The assertion source may only be enumerated once.")
                : _fault == PredicateIteratorFault.GetEnumerator ? throw _exception! : new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(PredicateTrackingEnumerable<T> owner) : IEnumerator<T>
        {
            private int _index = -1;

            public T Current => owner._fault == PredicateIteratorFault.Current ? throw owner._exception! : owner._values[_index];

            object? IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (owner._fault == PredicateIteratorFault.MoveNext)
                {
                    throw owner._exception!;
                }

                _index++;
                return _index < owner._values.Length;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
                owner.DisposeCalls++;
                owner._onDispose?.Invoke();
            }
        }
    }
}
