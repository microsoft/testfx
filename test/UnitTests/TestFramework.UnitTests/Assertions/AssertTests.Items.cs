// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void Count_WhenCountIsSame_ShouldPass()
    {
        var collection = new List<int> { 1, 2, 3 };
        Assert.HasCount(3, collection);
    }

    public void Count_InterpolatedString_WhenCountIsSame_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.HasCount(0, Array.Empty<string>(), $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void Count_WhenCountIsNotSame_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Action action = () => Assert.HasCount(3, collection);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain a specific number of elements.

                expected count: 3
                actual count:   1

                Assert.HasCount(3, collection)
                """);
    }

    public async Task Count_InterpolatedString_WhenCountIsNotSame_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        // Use a List<int> (not an array) so this exercises the IEnumerable<T> overload: arrays now
        // bind to the ReadOnlySpan<T> overload, and a span cannot be preserved across an await.
        var collection = new List<int>();
        Func<Task> action = async () => Assert.HasCount(1, collection, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage(
                $$"""
                Assertion failed. Expected collection to contain a specific number of elements.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {{string.Format(null, "{0:tt}", dateTime)}}, {{string.Format(null, "{0,5:tt}", dateTime)}}

                expected count: 1
                actual count:   0

                Assert.HasCount(1, collection)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void Count_WhenCurrentCultureUsesCustomNegativeSign_ShouldUseInvariantCallSiteValue()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        var customCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        customCulture.NumberFormat.NegativeSign = "−";

        try
        {
            CultureInfo.CurrentCulture = customCulture;
            var collection = new List<int>();

            Action action = () => Assert.HasCount(-1, collection);
            action.Should().Throw<Exception>()
                .WithMessage(
                    """
                    Assertion failed. Expected collection to contain a specific number of elements.

                    expected count: −1
                    actual count:   0

                    Assert.HasCount(-1, collection)
                    """);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    public void Count_ReadOnlySpan_WhenCountIsSame_ShouldPass()
    {
        int[] array = [1, 2, 3];
        ReadOnlySpan<int> span = array;
        Assert.HasCount(3, span);
    }

    public void Count_Span_WhenCountIsSame_ShouldPass()
    {
        int[] array = [1, 2, 3];
        Span<int> span = array;
        Assert.HasCount(3, span);
    }

    public void Count_Memory_WhenCountIsSame_ShouldPass()
    {
        int[] array = [1, 2, 3];
        Memory<int> memory = array;
        Assert.HasCount(3, memory);
    }

    public void Count_ReadOnlyMemory_WhenCountIsSame_ShouldPass()
    {
        int[] array = [1, 2, 3];
        ReadOnlyMemory<int> memory = array;
        Assert.HasCount(3, memory);
    }

    public void Count_ReadOnlySpan_WhenCountIsNotSame_ShouldFail()
    {
        int[] array = [1];
        ReadOnlySpan<int> span = array;
        // ReadOnlySpan cannot be captured by a lambda, so call directly inside a try/catch.
        Exception? exception = null;
        try
        {
            Assert.HasCount(3, span);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(
            """
            Assertion failed. Expected collection to contain a specific number of elements.

            expected count: 3
            actual count:   1

            Assert.HasCount(3, span)
            """);
    }

    public void Count_Memory_WhenCountIsNotSame_ShouldFail()
    {
        int[] array = [1];
        Memory<int> memory = array;
        Action action = () => Assert.HasCount(3, memory);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain a specific number of elements.

                expected count: 3
                actual count:   1

                Assert.HasCount(3, memory)
                """);
    }

    public void Count_ReadOnlySpan_InterpolatedString_WhenCountIsSame_ShouldNotEvaluateMessage()
    {
        DummyClassTrackingToStringCalls o = new();
        int[] array = [1, 2, 3];
        ReadOnlySpan<int> span = array;
        Assert.HasCount(3, span, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void Count_Memory_InterpolatedString_WhenCountIsNotSame_ShouldEvaluateMessageAndFail()
    {
        DummyClassTrackingToStringCalls o = new();
        int[] array = [1];
        Memory<int> memory = array;
        Action action = () => Assert.HasCount(3, memory, $"User-provided message: {o}");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain a specific number of elements.
                User-provided message: DummyClassTrackingToStringCalls

                expected count: 3
                actual count:   1

                Assert.HasCount(3, memory)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void Count_Span_WhenCountIsNotSame_ShouldFail()
    {
        int[] array = [1];
        Span<int> span = array;
        // Span cannot be captured by a lambda, so call directly inside a try/catch.
        Exception? exception = null;
        try
        {
            Assert.HasCount(3, span);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(
            """
            Assertion failed. Expected collection to contain a specific number of elements.

            expected count: 3
            actual count:   1

            Assert.HasCount(3, span)
            """);
    }

    public void Count_ReadOnlyMemory_WhenCountIsNotSame_ShouldFail()
    {
        int[] array = [1];
        ReadOnlyMemory<int> memory = array;
        Action action = () => Assert.HasCount(3, memory);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain a specific number of elements.

                expected count: 3
                actual count:   1

                Assert.HasCount(3, memory)
                """);
    }

    public void Count_Span_InterpolatedString_WhenCountIsNotSame_ShouldEvaluateMessageAndFail()
    {
        DummyClassTrackingToStringCalls o = new();
        int[] array = [1];
        Span<int> span = array;
        // Span cannot be captured by a lambda, so call directly inside a try/catch.
        Exception? exception = null;
        try
        {
            Assert.HasCount(3, span, $"User-provided message: {o}");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(
            """
            Assertion failed. Expected collection to contain a specific number of elements.
            User-provided message: DummyClassTrackingToStringCalls

            expected count: 3
            actual count:   1

            Assert.HasCount(3, span)
            """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void Count_ReadOnlyMemory_InterpolatedString_WhenCountIsNotSame_ShouldEvaluateMessageAndFail()
    {
        DummyClassTrackingToStringCalls o = new();
        int[] array = [1];
        ReadOnlyMemory<int> memory = array;
        Action action = () => Assert.HasCount(3, memory, $"User-provided message: {o}");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain a specific number of elements.
                User-provided message: DummyClassTrackingToStringCalls

                expected count: 3
                actual count:   1

                Assert.HasCount(3, memory)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void Count_Array_InterpolatedString_WhenCountIsSame_ShouldNotEvaluateMessage()
    {
        // Arrays bind to the ReadOnlySpan<T> overload now that it exists; the interpolated
        // message must still be evaluated lazily (i.e. only when the assertion fails).
        DummyClassTrackingToStringCalls o = new();
        Assert.HasCount(0, Array.Empty<string>(), $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void IsEmpty_ReadOnlySpan_WhenEmpty_ShouldPass()
    {
        ReadOnlySpan<int> span = [];
        Assert.IsEmpty(span);
    }

    public void IsEmpty_Span_WhenEmpty_ShouldPass()
    {
        Span<int> span = [];
        Assert.IsEmpty(span);
    }

    public void IsEmpty_Memory_WhenEmpty_ShouldPass()
    {
        Memory<int> memory = Array.Empty<int>();
        Assert.IsEmpty(memory);
    }

    public void IsEmpty_ReadOnlyMemory_WhenEmpty_ShouldPass()
    {
        ReadOnlyMemory<int> memory = Array.Empty<int>();
        Assert.IsEmpty(memory);
    }

    public void IsEmpty_ReadOnlySpan_InterpolatedString_WhenEmpty_ShouldNotEvaluateMessage()
    {
        DummyClassTrackingToStringCalls o = new();
        ReadOnlySpan<int> span = [];
        Assert.IsEmpty(span, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void IsEmpty_ReadOnlySpan_WhenNotEmpty_ShouldFail()
    {
        int[] array = [1];
        ReadOnlySpan<int> span = array;
        // ReadOnlySpan cannot be captured by a lambda, so call directly inside a try/catch.
        Exception? exception = null;
        try
        {
            Assert.IsEmpty(span);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(
            """
            Assertion failed. Expected collection to be empty.

            expected count: 0
            actual count:   1

            Assert.IsEmpty(span)
            """);
    }

    public void IsEmpty_Memory_WhenNotEmpty_ShouldFail()
    {
        int[] array = [1];
        Memory<int> memory = array;
        Action action = () => Assert.IsEmpty(memory);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be empty.

                expected count: 0
                actual count:   1

                Assert.IsEmpty(memory)
                """);
    }

    public void IsEmpty_Memory_InterpolatedString_WhenNotEmpty_ShouldEvaluateMessageAndFail()
    {
        int[] array = [1];
        Memory<int> memory = array;
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsEmpty(memory, $"User-provided message: {o}");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be empty.
                User-provided message: DummyClassTrackingToStringCalls

                expected count: 0
                actual count:   1

                Assert.IsEmpty(memory)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void IsNotEmpty_ReadOnlySpan_WhenNotEmpty_ShouldPass()
    {
        int[] array = [1];
        ReadOnlySpan<int> span = array;
        Assert.IsNotEmpty(span);
    }

    public void IsNotEmpty_Span_WhenNotEmpty_ShouldPass()
    {
        int[] array = [1];
        Span<int> span = array;
        Assert.IsNotEmpty(span);
    }

    public void IsNotEmpty_Memory_WhenNotEmpty_ShouldPass()
    {
        int[] array = [1];
        Memory<int> memory = array;
        Assert.IsNotEmpty(memory);
    }

    public void IsNotEmpty_ReadOnlyMemory_WhenNotEmpty_ShouldPass()
    {
        int[] array = [1];
        ReadOnlyMemory<int> memory = array;
        Assert.IsNotEmpty(memory);
    }

    public void IsNotEmpty_ReadOnlySpan_InterpolatedString_WhenNotEmpty_ShouldNotEvaluateMessage()
    {
        DummyClassTrackingToStringCalls o = new();
        int[] array = [1];
        ReadOnlySpan<int> span = array;
        Assert.IsNotEmpty(span, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void IsNotEmpty_ReadOnlySpan_WhenEmpty_ShouldFail()
    {
        ReadOnlySpan<int> span = [];
        // ReadOnlySpan cannot be captured by a lambda, so call directly inside a try/catch.
        Exception? exception = null;
        try
        {
            Assert.IsNotEmpty(span);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(
            """
            Assertion failed. Expected collection to not be empty.

            actual count: 0

            Assert.IsNotEmpty(span)
            """);
    }

    public void IsNotEmpty_Memory_WhenEmpty_ShouldFail()
    {
        Memory<int> memory = Array.Empty<int>();
        Action action = () => Assert.IsNotEmpty(memory);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be empty.

                actual count: 0

                Assert.IsNotEmpty(memory)
                """);
    }

    public void IsNotEmpty_Memory_InterpolatedString_WhenEmpty_ShouldEvaluateMessageAndFail()
    {
        Memory<int> memory = Array.Empty<int>();
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsNotEmpty(memory, $"User-provided message: {o}");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be empty.
                User-provided message: DummyClassTrackingToStringCalls

                actual count: 0

                Assert.IsNotEmpty(memory)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }
#endif

    public void NotAny_WhenEmpty_ShouldPass()
        => Assert.IsEmpty(Array.Empty<int>());

    public void NotAny_InterpolatedString_WhenEmpty_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsEmpty(Array.Empty<string>(), $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void NotAny_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Action action = () => Assert.IsEmpty(collection);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be empty.

                expected count: 0
                actual count:   1

                Assert.IsEmpty(collection)
                """);
    }

    public async Task NotAny_InterpolatedString_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsEmpty(collection, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage(
                $$"""
                Assertion failed. Expected collection to be empty.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {{string.Format(null, "{0:tt}", dateTime)}}, {{string.Format(null, "{0,5:tt}", dateTime)}}

                expected count: 0
                actual count:   1

                Assert.IsEmpty(collection)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void Single_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        int first = Assert.ContainsSingle(collection);
        (first == 1).Should().BeTrue();
    }

    public void Single_InterpolatedString_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        DummyClassTrackingToStringCalls o = new();
        Assert.ContainsSingle(collection, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void Single_WhenNoItems_ShouldFail()
    {
        Action action = () => Assert.ContainsSingle(Array.Empty<int>());
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element.*expected count:*1*actual count:*0*Assert.ContainsSingle(Array.Empty<int>())*");
    }

    public void Single_WhenMultipleItems_ShouldFail()
    {
        Action action = () => Assert.ContainsSingle([1, 2, 3]);
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element.*expected count:*1*actual count:*3*Assert.ContainsSingle([1, 2, 3])*");
    }

    public async Task Single_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.ContainsSingle(Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"*Expected collection to contain exactly one element.*User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}*expected count:*1*actual count:*0*Assert.ContainsSingle(Array.Empty<int>())*");
        o.WasToStringCalled.Should().BeTrue();
    }

    public async Task Single_InterpolatedString_WhenMultipleItems_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.ContainsSingle([1, 2, 3], $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"*Expected collection to contain exactly one element.*User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}*expected count:*1*actual count:*3*Assert.ContainsSingle([1, 2, 3])*");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void SinglePredicate_WhenOneItemMatches_ShouldPass()
    {
        var collection = new List<int> { 1, 3, 5 };
        int result = Assert.ContainsSingle(x => x == 3, collection);
        result.Should().Be(3);
    }

    public void SinglePredicate_WithMessage_WhenOneItemMatches_ShouldPass()
    {
        var collection = new List<string> { "apple", "banana", "cherry" };
#pragma warning disable CA1865 // Use char overload - not netfx
        string result = Assert.ContainsSingle(x => x.StartsWith("b", StringComparison.Ordinal), collection, "Expected one item starting with 'b'");
#pragma warning restore CA1865
        result.Should().Be("banana");
    }

    public void SinglePredicate_WhenNoItemMatches_ShouldFail()
    {
        var collection = new List<int> { 1, 3, 5 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection);
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element matching the predicate.*expected matches:*1*actual matches:*0*Assert.ContainsSingle(x => x % 2 == 0, collection)*");
    }

    public void SinglePredicate_WhenMultipleItemsMatch_ShouldFail()
    {
        var collection = new List<int> { 2, 4, 6 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection);
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element matching the predicate.*expected matches:*1*actual matches:*3*Assert.ContainsSingle(x => x % 2 == 0, collection)*");
    }

    public void SinglePredicate_Message_WhenNoItemMatches_ShouldFail()
    {
        var collection = new List<int> { 1, 3, 5 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, "No even numbers found: test");
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element matching the predicate.*No even numbers found: test*expected matches:*1*actual matches:*0*Assert.ContainsSingle(x => x % 2 == 0, collection)*");
    }

    public void SinglePredicate_Message_WhenMultipleItemsMatch_ShouldFail()
    {
        var collection = new List<int> { 2, 4, 6 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, "Too many even numbers: test");
        action.Should().Throw<Exception>()
            .WithMessage("*Expected collection to contain exactly one element matching the predicate.*Too many even numbers: test*expected matches:*1*actual matches:*3*Assert.ContainsSingle(x => x % 2 == 0, collection)*");
    }

    public void Any_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        Assert.IsNotEmpty(collection);
    }

    public void Any_WhenMultipleItems_ShouldPass()
    {
        var collection = new List<int> { 1, 2, 3 };
        Assert.IsNotEmpty(collection);
    }

    public void Any_InterpolatedString_WhenAnyOneItem_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNotEmpty([1], $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void Any_InterpolatedString_WhenMultipleItems_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNotEmpty([1, 2, 3], $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void Any_WhenNoItem_ShouldFail()
    {
        Action action = () => Assert.IsNotEmpty(Array.Empty<int>());
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be empty.

                actual count: 0

                Assert.IsNotEmpty(Array.Empty<int>())
                """);
    }

    public async Task Any_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        // Use a List<string> (not an array) so this exercises the IEnumerable<T> overload: arrays now
        // bind to the ReadOnlySpan<T> overload, and a span cannot be preserved across an await.
        var collection = new List<string>();
        Func<Task> action = async () => Assert.IsNotEmpty(collection, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage(
                $$"""
                Assertion failed. Expected collection to not be empty.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {{string.Format(null, "{0:tt}", dateTime)}}, {{string.Format(null, "{0,5:tt}", dateTime)}}

                actual count: 0

                Assert.IsNotEmpty(collection)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }
}
