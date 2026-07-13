// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether two object graphs are NOT structurally equivalent and throws an exception if they are.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not to match
    /// <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equivalent to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> and <paramref name="actual"/> are structurally equivalent,
    /// or if the structural comparison cannot be completed.
    /// </exception>
    /// <remarks>
    /// See <see cref="AreEquivalent{T}(T, T, string, string, string)"/> and
    /// <see cref="AreEquivalent{T}(T, T, bool, string, string, string)"/> for the full set of comparison rules.
    /// </remarks>
    public static void AreNotEquivalent<T>(T? notExpected, T? actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected, actual, strict: false, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two object graphs are NOT structurally equivalent and throws an exception if they are.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not to match
    /// <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="strict">
    /// When <see langword="true"/>, the equivalence check used to decide whether the assertion fails is
    /// performed in strict mode (extra members or dictionary keys on <paramref name="actual"/> are
    /// considered a difference, so the assertion succeeds in their presence).
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equivalent to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> and <paramref name="actual"/> are structurally equivalent,
    /// or if the structural comparison cannot be completed.
    /// </exception>
    public static void AreNotEquivalent<T>(T? notExpected, T? actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEquivalent");
        EquivalenceComparer comparer = new(strict);
        EquivalenceMismatch? mismatch = comparer.Compare(notExpected, actual);
        if (mismatch is null)
        {
            ReportAssertAreNotEquivalentFailed(notExpected, actual, strict, message, notExpectedExpression, actualExpression);
        }
        else if (mismatch.IsComparisonFailure)
        {
            ReportAssertAreNotEquivalentComparisonFailed(mismatch, strict, message, notExpectedExpression, actualExpression);
        }
    }

#if NETCOREAPP3_1_OR_GREATER

    #region AreNotEquivalent span/memory

    // NOTE: The span overloads below delegate through `.ToArray()` to the same-named `AreNotEquivalent`
    // method. This does NOT recurse: `.ToArray()` produces a `T[]`, which is an exact match for the single-value
    // `AreNotEquivalent<T[]>(T?, T?, ...)` overload. An exact match outranks the implicit `T[]`-to-`ReadOnlySpan<T>`
    // conversion during overload resolution, so the call binds to the array-based deep-comparison overload rather
    // than back to the span overload. The `ReadOnlyMemory`/`Memory` overloads similarly delegate via `.Span` to the
    // corresponding `ReadOnlySpan` overload.

    /// <summary>
    /// Tests whether two spans are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="notExpected">The span the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.ToArray(), actual.ToArray(), strict: false, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="notExpected">The span the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.ToArray(), actual.ToArray(), strict, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="notExpected">The span the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(Span<T> notExpected, Span<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.ToArray(), actual.ToArray(), strict: false, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="notExpected">The span the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(Span<T> notExpected, Span<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.ToArray(), actual.ToArray(), strict, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="notExpected">The memory the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.Span, actual.Span, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="notExpected">The memory the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.Span, actual.Span, strict, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="notExpected">The memory the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(Memory<T> notExpected, Memory<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.Span, actual.Span, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are NOT structurally equivalent.
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="notExpected">The memory the test expects not to match <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreNotEquivalent<T>(Memory<T> notExpected, Memory<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEquivalent(notExpected.Span, actual.Span, strict, message, notExpectedExpression, actualExpression);

    #endregion // AreNotEquivalent span/memory

#endif

    [DoesNotReturn]
    private static void ReportAssertAreNotEquivalentComparisonFailed(EquivalenceMismatch mismatch, bool strict, string? userMessage, string notExpectedExpression, string actualExpression)
    {
        string summary = strict
            ? FrameworkMessages.AreNotEquivalentComparisonFailedSummaryStrict
            : FrameworkMessages.AreNotEquivalentComparisonFailedSummary;

        ReportAssertEquivalenceMismatch(
            mismatch,
            summary,
            userMessage,
            "Assert.AreNotEquivalent",
            notExpectedExpression,
            actualExpression,
            "<notExpected>",
            "<actual>",
            "not expected:",
            "actual:");
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotEquivalentFailed<T>(T? notExpected, T? actual, bool strict, string? userMessage, string notExpectedExpression, string actualExpression)
    {
        string summary = strict
            ? FrameworkMessages.AreNotEquivalentFailedSummaryStrict
            : FrameworkMessages.AreNotEquivalentFailedSummary;

        string actualText = AssertionValueRenderer.RenderValue(actual);
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpected);

        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("not expected:", notExpectedText)
            .AddLine("actual:", actualText);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual($"not equivalent to {notExpectedText}", actualText);

        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreNotEquivalent", notExpectedExpression, actualExpression, "<notExpected>", "<actual>"));

        ReportAssertFailed(structured);
    }
}
