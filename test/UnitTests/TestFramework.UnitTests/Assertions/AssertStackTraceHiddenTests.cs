// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Verifies that frames produced by <see cref="Assert"/>, <see cref="CollectionAssert"/>,
/// and <see cref="StringAssert"/> are stripped from <see cref="Exception.StackTrace"/> on
/// runtimes that honor <c>[StackTraceHidden]</c> (.NET 6+). On older runtimes (.NET
/// Framework / netstandard2.0 consumers), the attribute is a no-op and the frames remain
/// visible; these tests are therefore only meaningful on the modern runtime and are gated
/// accordingly.
/// </summary>
public class AssertStackTraceHiddenTests : TestContainer
{
    public void AssertAreEqualFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(() => Assert.AreEqual(1, 2));

        AssertHidesFrameworkFrames(exception);
    }

    public void AssertIComparableFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(() => Assert.IsGreaterThan(1, 0));

        AssertHidesFrameworkFrames(exception);
    }

    public void AssertIsNullFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(() => Assert.IsNull(new object()));

        AssertHidesFrameworkFrames(exception);
    }

    public void AssertFailFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(() => Assert.Fail());

        AssertHidesFrameworkFrames(exception);
    }

    public void CollectionAssertFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(
            () => CollectionAssert.AreEqual(new[] { 1 }, new[] { 2 }));

        AssertHidesFrameworkFrames(exception);
    }

    public void StringAssertFailure_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(
            () => StringAssert.Contains("hello", "world"));

        AssertHidesFrameworkFrames(exception);
    }

    public void AssertIsNullInterpolatedHandlerFailure_StackTraceHidesFrameworkFrame()
    {
        int value = 42;
        AssertFailedException exception = CaptureFailure(
            () => Assert.IsNull(new object(), $"value was {value}"));

        AssertHidesFrameworkFrames(exception);
    }

    public void AssertFailureInsideScope_StackTraceHidesFrameworkFrame()
    {
        AssertFailedException exception = CaptureFailure(() =>
        {
            using (Assert.Scope())
            {
                Assert.AreEqual(1, 2);
            }
        });

        AssertHidesFrameworkFrames(exception);
    }

    private static AssertFailedException CaptureFailure(Action action)
    {
        try
        {
            action();
        }
        catch (AssertFailedException ex)
        {
            return ex;
        }

        throw new InvalidOperationException("Expected AssertFailedException was not thrown.");
    }

    private static void AssertHidesFrameworkFrames(Exception exception)
    {
#if NET6_0_OR_GREATER
        string? stackTrace = exception.StackTrace;
        stackTrace.Should().NotBeNull();

        // No assertion-framework method should leak into the captured stack trace.
        // The runtime should have stripped every method on Assert / CollectionAssert /
        // StringAssert / AssertExtensions (and on the nested interpolated string
        // handlers and the AssertScope path) because they are all annotated with
        // [StackTraceHidden].
        string[] forbiddenTypePrefixes =
        [
            "Microsoft.VisualStudio.TestTools.UnitTesting.Assert.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.Assert+",
            "Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertExtensions.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertScope.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.StructuredAssertionMessage.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.AssertionValueRenderer.",
            "Microsoft.VisualStudio.TestTools.UnitTesting.EvidenceBlock.",
        ];

        foreach (string prefix in forbiddenTypePrefixes)
        {
            stackTrace!.Should().NotContain(
                prefix,
                because: $"frames whose declaring type starts with '{prefix}' should be hidden by [StackTraceHidden] on .NET 6+ (actual stack trace: {Environment.NewLine}{stackTrace})");
        }
#else
        // [StackTraceHidden] is a no-op on .NET Framework / netstandard2.0 hosts. The exception is
        // still produced; we just don't assert anything about the stack contents here.
        exception.Should().NotBeNull();
#endif
    }
}
