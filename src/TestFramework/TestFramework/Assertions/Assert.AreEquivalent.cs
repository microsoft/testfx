// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether two object graphs are structurally equivalent (deep equality) and throws an
    /// exception if they are not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equivalent to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not structurally equivalent.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This assertion performs a deep, order-sensitive structural comparison of two object graphs.
    /// It differs from <see cref="CollectionAssert.AreEquivalent(System.Collections.ICollection?, System.Collections.ICollection?)"/>,
    /// which checks only top-level collection equivalence without regard to element order.
    /// </para>
    /// <para>
    /// The comparison rules are:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     Reference-equal values (including both <see langword="null"/>) are considered equivalent.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Primitive-like types (numerics, <see cref="bool"/>, <see cref="char"/>, <see cref="string"/>,
    ///     enums, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>,
    ///     <see cref="Guid"/>, <see cref="Uri"/>, <see cref="Type"/>, <see cref="Version"/>) are compared with
    ///     <see cref="object.Equals(object?)"/>. On target frameworks that ship them,
    ///     <c>DateOnly</c>, <c>TimeOnly</c>, <c>Half</c>, <c>Int128</c>, and <c>UInt128</c> are also
    ///     treated as primitive-like.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     If the static (compile-time) type of a value implements <see cref="IEquatable{T}"/> for itself,
    ///     <see cref="IEquatable{T}.Equals(T)"/> is used and recursion stops at that point.
    ///     Plain <see cref="object.Equals(object?)"/> overrides are ignored — the comparer always
    ///     recurses into members for those.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Dictionaries (<see cref="IDictionary"/>, <see cref="IDictionary{TKey, TValue}"/>, or
    ///     <see cref="IReadOnlyDictionary{TKey, TValue}"/>) are compared by key set, with values compared
    ///     recursively. Lookups are routed through the source dictionary so its
    ///     <see cref="IEqualityComparer{T}"/> for keys is preserved.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Other enumerables (excluding <see cref="string"/>) are compared element-by-element in the
    ///     iteration order. Comparison is <b>order-sensitive</b>; if order is irrelevant, use
    ///     <see cref="CollectionAssert.AreEquivalent(System.Collections.ICollection?, System.Collections.ICollection?)"/>
    ///     on the collection itself.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Other reference types are compared by recursing into all public instance properties (with a public
    ///     getter and no index parameters) and public instance fields. Static, non-public, and indexed members
    ///     are ignored.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Reference cycles are tracked and traversed at most once per (expected, actual) pair. The same
    ///     mechanism enforces graph topology: if the same reference on one side is paired with different
    ///     references on the other side, the assertion fails.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     If a user-defined <see cref="IEquatable{T}.Equals(T)"/>, member getter, or dictionary
    ///     access (<c>TryGetValue</c>, <c>ContainsKey</c>, or enumeration) throws while being
    ///     evaluated, the assertion fails with a message describing the exception type and message.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Comparison stops after a bounded recursion depth to avoid <see cref="StackOverflowException"/>
    ///     on unusually deep object graphs, and reports that condition as an assertion failure.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    public static void AreEquivalent<T>(T? expected, T? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected, actual, strict: false, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two object graphs are structurally equivalent (deep equality) and throws an
    /// exception if they are not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="strict">
    /// When <see langword="true"/>, the comparison fails if <paramref name="actual"/> declares any
    /// public properties or fields that are not present on <paramref name="expected"/>'s runtime type,
    /// or if any extra dictionary keys are present on <paramref name="actual"/>. When <see langword="false"/>
    /// (the default), additional members and dictionary keys on <paramref name="actual"/> are ignored.
    /// When the runtime types of <paramref name="expected"/> and <paramref name="actual"/> are identical,
    /// this flag has no effect on the public-member comparison, but it still rejects extra dictionary keys
    /// on <paramref name="actual"/> when comparing dictionaries.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equivalent to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not structurally equivalent.
    /// </exception>
    public static void AreEquivalent<T>(T? expected, T? actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreEquivalent");
        EquivalenceComparer comparer = new(strict);
        EquivalenceMismatch? mismatch = comparer.Compare(expected, actual);
        if (mismatch is null)
        {
            return;
        }

        ReportAssertAreEquivalentFailed(mismatch, strict, message, expectedExpression, actualExpression);
    }

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

    #region AreEquivalent span/memory

    /// <summary>
    /// Tests whether two spans are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="expected">The span the test expects.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.ToArray(), actual.ToArray(), strict: false, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="expected">The span the test expects.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.ToArray(), actual.ToArray(), strict, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="expected">The span the test expects.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(Span<T> expected, Span<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.ToArray(), actual.ToArray(), strict: false, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the spans.</typeparam>
    /// <param name="expected">The span the test expects.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(Span<T> expected, Span<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.ToArray(), actual.ToArray(), strict, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="expected">The memory the test expects.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.Span, actual.Span, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="expected">The memory the test expects.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.Span, actual.Span, strict, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="expected">The memory the test expects.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(Memory<T> expected, Memory<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.Span, actual.Span, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions are structurally equivalent (deep, order-sensitive element comparison).
    /// </summary>
    /// <typeparam name="T">The element type of the memory regions.</typeparam>
    /// <param name="expected">The memory the test expects.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="strict">When <see langword="true"/>, the comparison is performed in strict mode.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreEquivalent<T>(Memory<T> expected, Memory<T> actual, bool strict, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEquivalent(expected.Span, actual.Span, strict, message, expectedExpression, actualExpression);

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

    #endregion // AreEquivalent span/memory

#endif

    [DoesNotReturn]
    private static void ReportAssertAreEquivalentFailed(EquivalenceMismatch mismatch, bool strict, string? userMessage, string expectedExpression, string actualExpression)
    {
        string summary = strict
            ? FrameworkMessages.AreEquivalentFailedSummaryStrict
            : FrameworkMessages.AreEquivalentFailedSummary;

        ReportAssertEquivalenceMismatch(
            mismatch,
            summary,
            userMessage,
            "Assert.AreEquivalent",
            expectedExpression,
            actualExpression,
            "<expected>",
            "<actual>",
            "expected:",
            "actual:");
    }

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
    private static void ReportAssertEquivalenceMismatch(EquivalenceMismatch mismatch, string summary, string? userMessage, string assertionMethodName, string leftExpression, string rightExpression, string leftPlaceholder, string rightPlaceholder, string leftLabel, string rightLabel)
    {
        string locationLine = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreEquivalentMismatchAt,
            mismatch.Path.Length == 0 ? FrameworkMessages.AreEquivalentRootPath : mismatch.Path,
            mismatch.Reason);

        StructuredAssertionMessage structured = new(summary);
        structured.WithAdditionalSummaryLine(locationLine);
        structured.WithUserMessage(userMessage);

        if (mismatch.ExpectedText is not null && mismatch.ActualText is not null)
        {
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine(leftLabel, mismatch.ExpectedText)
                .AddLine(rightLabel, mismatch.ActualText);
            structured.WithEvidence(evidence);
            structured.WithExpectedAndActual(mismatch.ExpectedText, mismatch.ActualText);
        }

        structured.WithCallSiteExpression(FormatCallSiteExpression(assertionMethodName, leftExpression, rightExpression, leftPlaceholder, rightPlaceholder));

        ReportAssertFailed(structured);
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
