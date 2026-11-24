// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

public partial class AssertTests : TestContainer
{
    public void DoesNotContain_Predicate_EmptyCollections_DoesNotEvaluatePredicate()
        => VerifyEmptyPredicateCollections(containsOnly: false);

    public void DoesNotContain_Predicate_NoMatchingItems_EvaluatesEachItemOnce()
        => VerifySuccessfulPredicateVisits(containsOnly: false);

    public void DoesNotContain_Predicate_FirstMatch_ReportsIndexValueAndExpressions()
        => VerifyFirstPredicateCounterexample(containsOnly: false);

    public void DoesNotContain_Predicate_LaterMatch_StopsAtFirstCounterexample()
        => VerifyLaterPredicateCounterexample(containsOnly: false);

    public void DoesNotContain_Predicate_NullElement_IsPassedToPredicateAndReported()
        => VerifyNullPredicateItems(containsOnly: false);

    public void DoesNotContain_Predicate_SingleUseEnumerable_EnumeratesOnceAndDisposes()
        => VerifySingleUsePredicateEnumerable(containsOnly: false);

    public void DoesNotContain_Predicate_CounterexampleValue_IsCapturedBeforeEnumeratorDisposal()
        => VerifyMutablePredicateCounterexample(containsOnly: false);

    public void DoesNotContain_Predicate_NestedEnumerableItem_IsNotEnumeratedForDiagnostics()
        => VerifyNestedPredicateCounterexample(containsOnly: false);

    public void DoesNotContain_Predicate_CustomValueFormatters_ControlCounterexampleRendering()
        => VerifyCustomPredicateCounterexampleFormatters(containsOnly: false);

    public void DoesNotContain_Predicate_PredicateException_PropagatesOriginalInstanceAndDisposes()
        => VerifyPredicateExceptionPropagation(containsOnly: false);

    public void DoesNotContain_Predicate_IteratorExceptions_PropagateOriginalInstance()
        => VerifyPredicateIteratorExceptions(containsOnly: false);

    public void DoesNotContain_Predicate_CancellationExceptions_PropagateUnchanged()
        => VerifyPredicateCancellationPropagation(containsOnly: false);

    public void DoesNotContain_Predicate_GenericInvalidArguments_PreserveArgumentNullException()
    {
        foreach (PredicateCollectionShape shape in new[] { PredicateCollectionShape.GenericEnumerable, PredicateCollectionShape.Array, PredicateCollectionShape.Collection })
        {
            Func<int, bool> predicate = value => value > 0;
            Action nullCollection = () => InvokePredicateCollection<int>(false, shape, predicate, null);
            Action bothNull = () => InvokePredicateCollection<int>(false, shape, null, null);

            nullCollection.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("source");
            bothNull.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("source");
            foreach (int[] values in new int[][] { [], [1] })
            {
                Action nullPredicate = () => InvokePredicateCollection<int>(false, shape, null, values);
                nullPredicate.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("predicate");
            }
        }
    }

    public void DoesNotContain_Predicate_NonGenericEmptyCollection_RejectsNullPredicate()
    {
        // Existing nongeneric tests already pin nonempty/null-collection failures. An empty
        // nongeneric source must still reject null, unlike the existing empty span contract.
        Action action = () => InvokePredicateCollection<int>(false, PredicateCollectionShape.NonGenericEnumerable, null, []);

        action.Should().ThrowExactly<AssertFailedException>().Which.Message
            .Should().Be(PredicateParameterMessage("DoesNotContain", "predicate"));
    }

#if NETCOREAPP3_1_OR_GREATER
    public void DoesNotContain_Predicate_SpanMemoryNullPredicate_PreservesExistingBehavior()
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes().Skip(4))
        {
            Action empty = () => InvokePredicateCollection<int>(false, shape, null, []);
            Action nonempty = () => InvokePredicateCollection<int>(false, shape, null, [1]);

            empty.Should().NotThrow();
            nonempty.Should().ThrowExactly<NullReferenceException>();
        }
    }

    public void DoesNotContain_Predicate_SpanMemorySlices_ReportRelativeIndexAndExcludeOutsideItems()
        => VerifyPredicateSlices(containsOnly: false);
#endif

    public void PredicateAssertions_ScopedFailures_AggregateAndContinue()
    {
        foreach (PredicateCollectionShape shape in GetPredicateCollectionShapes())
        {
            int continued = 0;
            List<int> onlyVisits = [];
            List<int> noneVisits = [];
            Func<int, bool> onlyPredicate = value =>
            {
                onlyVisits.Add(value);
                return value > 0;
            };
            Func<int, bool> nonePredicate = value =>
            {
                noneVisits.Add(value);
                return value > 0;
            };
            Action action = () =>
            {
                using (Assert.Scope())
                {
                    InvokePredicateCollection(true, shape, onlyPredicate, [-1, 2], "only context");
                    InvokePredicateCollection(false, shape, nonePredicate, [-1, 2], "none context");
                    continued++;
                    Assert.AreEqual(1, 1);
                }
            };

            AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
            failure.Message.Should().Be("2 assertion(s) failed within the assert scope.");
            AggregateException aggregate = failure.InnerException.Should().BeOfType<AggregateException>().Which;
            aggregate.InnerExceptions.Should().HaveCount(2);
            aggregate.InnerExceptions[0].Should().BeOfType<AssertFailedException>().Which.Message.Replace("\r\n", "\n")
                .Should().Be(PredicateCounterexampleMessage(true, 0, "-1", "only context").Replace("\r\n", "\n"));
            aggregate.InnerExceptions[1].Should().BeOfType<AssertFailedException>().Which.Message.Replace("\r\n", "\n")
                .Should().Be(PredicateCounterexampleMessage(false, 1, "2", "none context").Replace("\r\n", "\n"));
            var onlyFailure = (AssertFailedException)aggregate.InnerExceptions[0];
            var noneFailure = (AssertFailedException)aggregate.InnerExceptions[1];
            onlyFailure.ExpectedText.Should().Be("true");
            onlyFailure.ActualText.Should().Be("false");
            noneFailure.ExpectedText.Should().Be("false");
            noneFailure.ActualText.Should().Be("true");
            continued.Should().Be(1);
            onlyVisits.Should().Equal(-1);
            noneVisits.Should().Equal(-1, 2);
        }
    }
}
