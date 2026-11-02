// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
#if NETFRAMEWORK
using System.Runtime.Serialization;
#endif

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// TestResult object to be returned to adapter.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
public class TestResult
{
    /// <summary>
    /// Number of assertion failures carrying comparison values (an expected and/or actual text) observed across
    /// all <see cref="TestFailureException"/> assignments, capped at 2. A single comparison can always be
    /// attributed to the result; two or more compete, so none is reported.
    /// </summary>
    /// <remarks>
    /// Failures that carry no comparison — <c>Assert.Fail</c>, or any non-assertion exception — deliberately do
    /// not count. They add nothing that could be confused with the surviving pair, and counting them would make
    /// a cleanup <c>Assert.Fail</c> suppress the body's diff while a cleanup <c>throw</c> would not.
    /// <para>
    /// Deliberately serialized on .NET Framework, unlike <see cref="TestFailureException"/>: the values this
    /// guards (<see cref="ExceptionExpectedText"/> / <see cref="ExceptionActualText"/>) do cross an AppDomain
    /// boundary, so the count that governs them must cross too or the invariant is lost on the far side. It is
    /// optional so a payload written by a TestFramework build that predates this field still deserializes,
    /// defaulting to zero.
    /// </para>
    /// </remarks>
#if NETFRAMEWORK
    [OptionalField]
#endif
    private int _assertionComparisonCount;

    /// <summary>
    /// Gets or sets the display name of the result. Useful when returning multiple results.
    /// If null then Method name is used as DisplayName.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the outcome of the test execution.
    /// </summary>
    public UnitTestOutcome Outcome { get; set; }

    internal string? IgnoreReason { get; set; }

    // NOTE: On .NET Framework, TestResult can cross appdomain boundary, so the exception should generally be serializable.
    // But that's not always the case and we can't see good guarantees.
    // Alternatively, we set ExceptionMessage and ExceptionStackTrace, and serialize those instead of the exception.
    // That means, after crossing app domain, you shouldn't access TestFailureException.
    // On modern .NET targets there are no AppDomains, so [Serializable]/[NonSerialized] are not needed.

    /// <summary>
    /// Gets or sets the exception thrown when test is failed.
    /// </summary>
#if NETFRAMEWORK
    [field: NonSerialized]
#endif
    public Exception? TestFailureException
    {
        get
        {
            if ((ExceptionMessage is not null || ExceptionStackTrace is not null) && field is null)
            {
                // That means this property is accessed after crossing appdomain boundary.
                // So, we fail.
                throw new InvalidOperationException();
            }

            return field;
        }

        set
        {
            if (value is null)
            {
                // If the field is already null, we don't need to do anything.
                // If the field is non-null, it means we are trying to clear an exception, which is something we shouldn't do.
                // If it happened that we attempted to set it to null after it was non-null, we return and do
                // nothing. This is better than potentially masking real failures silently.
                Debug.Assert(field is null, "TestFailureException should not be set to null after it was non-null");
                return;
            }

            field = field is null
                ? value
                : field is AggregateException aggregateException
                    ? new AggregateException(aggregateException.InnerExceptions.Concat([value]))
                    : new AggregateException(field, value);

            ExceptionMessage = field.Message;
            ExceptionStackTrace = field.StackTrace;

            // Capture the structured assertion values as strings, for the same reason ExceptionMessage and
            // ExceptionStackTrace are captured: the exception instance itself does not survive an AppDomain
            // boundary, and the adapter reports results from these strings rather than from the exception.
            //
            // Only an unambiguous single comparison is surfaced. This setter can be called more than once
            // (e.g. a TestInitialize failure followed by a TestCleanup failure) and a single exception can
            // itself report several failures, so the count is accumulated across both.
            if (_assertionComparisonCount < 2)
            {
                int newComparisons = FindAssertionTexts(value, out string? expectedText, out string? actualText);
                if (newComparisons > 0)
                {
                    bool isOnlyComparison = _assertionComparisonCount == 0 && newComparisons == 1;
                    ExceptionExpectedText = isOnlyComparison ? expectedText : null;
                    ExceptionActualText = isOnlyComparison ? actualText : null;
                    _assertionComparisonCount = Math.Min(2, _assertionComparisonCount + newComparisons);
                }
            }
        }
    }

    internal string? ExceptionMessage { get; set; }

    internal string? ExceptionStackTrace { get; set; }

    /// <summary>
    /// Gets or sets the pre-formatted <c>expected</c> text of the assertion that failed the test, when the
    /// failure came from an assertion that has a natural expected value.
    /// </summary>
    /// <remarks>
    /// Optional on .NET Framework so a payload written by a TestFramework build that predates this member still
    /// deserializes, defaulting to <see langword="null"/>.
    /// </remarks>
#if NETFRAMEWORK
    [field: OptionalField]
#endif
    internal string? ExceptionExpectedText { get; set; }

    /// <summary>
    /// Gets or sets the pre-formatted <c>actual</c> text of the assertion that failed the test, when the
    /// failure came from an assertion that has a natural actual value.
    /// </summary>
    /// <remarks>
    /// Optional on .NET Framework so a payload written by a TestFramework build that predates this member still
    /// deserializes, defaulting to <see langword="null"/>.
    /// </remarks>
#if NETFRAMEWORK
    [field: OptionalField]
#endif
    internal string? ExceptionActualText { get; set; }

    /// <summary>
    /// Gets or sets the output of the message logged by test code.
    /// </summary>
    public string? LogOutput { get; set; }

    /// <summary>
    /// Gets or sets the output of the message logged by test code.
    /// </summary>
    public string? LogError { get; set; }

    /// <summary>
    /// Gets or sets the debug traces by test code.
    /// </summary>
    public string? DebugTrace { get; set; }

    /// <summary>
    /// Gets or sets the debug traces by test code.
    /// </summary>
    public string? TestContextMessages { get; set; }

    /// <summary>
    /// Gets or sets the execution id of the result.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the parent execution id of the result.
    /// </summary>
    public Guid ParentExecId { get; set; }

    /// <summary>
    /// Gets or sets the inner results count of the result.
    /// </summary>
    [Obsolete("This API is unused and has no effect.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int InnerResultsCount { get; set; }

    /// <summary>
    /// Gets or sets the duration of test execution.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the data row index in data source. Set only for results of individual
    /// run of data row of a data driven test.
    /// </summary>
    [Obsolete("This API is unused and has no effect.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int DatarowIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the return value of the test method. (Currently null always).
    /// </summary>
    [Obsolete("This API is unused and has no effect.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object? ReturnValue { get; set; }

    /// <summary>
    /// Gets or sets the result files attached by the test.
    /// </summary>
    public IList<string>? ResultFiles { get; set; }

    // UnitTestElement is not part of TestFramework, so we don't have strong typing here and we use object instead.
    // The value of this property should either be null, or be of type UnitTestElement.
    internal object? AssociatedUnitTestElement { get; set; }

    /// <summary>
    /// Gets or sets the 1-based attempt this result belongs to when the test method is decorated with a
    /// <see cref="RetryBaseAttribute"/>. The first (non-retry) execution is attempt 1.
    /// </summary>
    /// <remarks>
    /// Every attempt is reported to Microsoft.Testing.Platform so tooling can surface the in-process retry (see
    /// the <c>RetryAttemptProperty</c> platform property); the VSTest host receives only the final result.
    /// Results that are not part of a retry sequence keep the default value of 1, which reports as "no retry
    /// happened" everywhere.
    /// <para>
    /// Optional on .NET Framework so a payload written by a TestFramework build that predates this member still
    /// deserializes. Note the field then defaults to 0 rather than 1, because the initializer does not run during
    /// deserialization; every consumer treats a non-superseded result as the final outcome regardless of the
    /// number, so an older payload still reports as "no retry happened".
    /// </para>
    /// </remarks>
#if NETFRAMEWORK
    [field: OptionalField]
#endif
    internal int RetryAttemptNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether a later retry attempt superseded this result, so it is not the
    /// test's final outcome. Consumers that want exactly one result per test (VSTest, TRX, JUnit, the process
    /// exit code) ignore superseded results.
    /// </summary>
    /// <remarks>
    /// Optional on .NET Framework so a payload written by a TestFramework build that predates this member still
    /// deserializes, defaulting to <see langword="false"/> - i.e. treated as the test's final outcome.
    /// </remarks>
#if NETFRAMEWORK
    [field: OptionalField]
#endif
    internal bool IsSupersededRetryAttempt { get; set; }

    internal static TestResult CreateIgnoredResult(string? ignoreReason)
        => new()
        {
            Outcome = UnitTestOutcome.Ignored,
            IgnoreReason = ignoreReason,
        };

    /// <summary>
    /// Counts the assertion failures that carry comparison values (capped at 2) and reports the values of the
    /// first one.
    /// </summary>
    /// <remarks>
    /// The adapter wraps the original assertion exception in its own exception type, so the assertion the user
    /// cares about is usually not the outermost one. The count matters because <c>assert.expected</c> /
    /// <c>assert.actual</c> carry no label: two competing comparisons cannot both be reported and the surviving
    /// one would look authoritative. Soft assertions (<c>Assert.Scope</c>) reach this shape by throwing a single
    /// <see cref="AssertFailedException"/> wrapping an <see cref="AggregateException"/> of every collected
    /// failure. The per-assertion values remain visible in the failure message itself.
    /// </remarks>
    /// <returns>The number of comparisons found, saturating at 2.</returns>
    private static int FindAssertionTexts(Exception? exception, out string? expectedText, out string? actualText)
    {
        expectedText = null;
        actualText = null;
        int comparisonCount = 0;
        Visit(exception, 0, ref comparisonCount, ref expectedText, ref actualText);

        if (comparisonCount != 1)
        {
            expectedText = null;
            actualText = null;
        }

        return comparisonCount;

        static void Visit(Exception? exception, int depth, ref int comparisonCount, ref string? expectedText, ref string? actualText)
        {
            // Bound the walk so a pathologically deep exception chain cannot stall the test host. The depth is
            // carried into the recursion so nesting cannot escape the bound.
            const int MaxDepth = 10;

            while (exception is not null && depth < MaxDepth && comparisonCount < 2)
            {
                if (exception is AssertFailedException assertFailedException
                    && (assertFailedException.ExpectedText is not null || assertFailedException.ActualText is not null))
                {
                    comparisonCount++;
                    if (expectedText is null && actualText is null)
                    {
                        expectedText = assertFailedException.ExpectedText;
                        actualText = assertFailedException.ActualText;
                    }
                }

                if (exception is AggregateException aggregateException)
                {
                    foreach (Exception innerException in aggregateException.InnerExceptions)
                    {
                        Visit(innerException, depth + 1, ref comparisonCount, ref expectedText, ref actualText);
                        if (comparisonCount > 1)
                        {
                            return;
                        }
                    }

                    return;
                }

                exception = exception.InnerException;
                depth++;
            }
        }
    }
}
