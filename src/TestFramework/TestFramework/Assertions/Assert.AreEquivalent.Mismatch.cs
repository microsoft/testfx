// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    /// <summary>
    /// A single structural mismatch found by <see cref="EquivalenceComparer"/>, carrying the dotted
    /// member path, a localized reason summary, and any expected/actual snippets to render.
    /// </summary>
    private sealed class EquivalenceMismatch
    {
        private EquivalenceMismatch(string path, string reason, string? expectedText, string? actualText, bool isComparisonFailure)
        {
            Path = path;
            Reason = reason;
            ExpectedText = expectedText;
            ActualText = actualText;
            IsComparisonFailure = isComparisonFailure;
        }

        internal string Path { get; }

        internal string Reason { get; }

        internal string? ExpectedText { get; }

        internal string? ActualText { get; }

        internal bool IsComparisonFailure { get; }

        internal static EquivalenceMismatch ValueMismatch(string path, object? expected, object? actual)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchValue,
                AssertionValueRenderer.RenderValue(expected),
                AssertionValueRenderer.RenderValue(actual),
                isComparisonFailure: false);

        internal static EquivalenceMismatch NullMismatch(string path, object? expected, object? actual)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchNull,
                AssertionValueRenderer.RenderValue(expected),
                AssertionValueRenderer.RenderValue(actual),
                isComparisonFailure: false);

        internal static EquivalenceMismatch TypeMismatch(string path, Type expectedType, Type actualType)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchType, expectedType.FullName ?? expectedType.Name, actualType.FullName ?? actualType.Name),
                expectedType.FullName ?? expectedType.Name,
                actualType.FullName ?? actualType.Name,
                isComparisonFailure: false);

        internal static EquivalenceMismatch TopologyMismatch(string path)
            => new(
                path,
                FrameworkMessages.AreEquivalentMismatchTopology,
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch LengthMismatch(string path, int expectedCount, int actualCount)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchLength, expectedCount, actualCount),
                expectedCount.ToString(CultureInfo.InvariantCulture),
                actualCount.ToString(CultureInfo.InvariantCulture),
                isComparisonFailure: false);

        internal static EquivalenceMismatch MissingKey(string path, object key)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMissingKey, AssertionValueRenderer.RenderValue(key)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch UnexpectedKey(string path, object key)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchUnexpectedKey, AssertionValueRenderer.RenderValue(key)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch MissingMember(string path, string memberName)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMissingMember, memberName),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch ExtraMembers(string path, IReadOnlyList<string> extras)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchExtraMembers, string.Join(", ", extras)),
                expectedText: null,
                actualText: null,
                isComparisonFailure: false);

        internal static EquivalenceMismatch IEquatableThrew(string path, Exception thrown)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEquivalentMismatchIEquatableThrew,
                    thrown.GetType().Name,
                    thrown.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch DictionaryAccessFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedDictionaryThrew : FrameworkMessages.AreEquivalentMismatchActualDictionaryThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch EnumerationFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedEnumerationThrew : FrameworkMessages.AreEquivalentMismatchActualEnumerationThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch MemberAccessFailure(string path, bool isExpected, Exception inner)
            => new(
                path,
                string.Format(
                    CultureInfo.CurrentCulture,
                    isExpected ? FrameworkMessages.AreEquivalentMismatchExpectedMemberThrew : FrameworkMessages.AreEquivalentMismatchActualMemberThrew,
                    inner.GetType().Name,
                    inner.Message),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);

        internal static EquivalenceMismatch MaxDepthExceeded(string path, int maxDepth)
            => new(
                path,
                string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEquivalentMismatchMaxDepth, maxDepth),
                expectedText: null,
                actualText: null,
                isComparisonFailure: true);
    }
}
