// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.HasCount failed. Expected collection of size 3. Actual: 1. 'collection' expression: 'collection'.");
    }

    public async Task Count_InterpolatedString_WhenCountIsNotSame_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.HasCount(1, Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).And
            .Message.Should().Be($"Assert.HasCount failed. Expected collection of size 1. Actual: 0. 'collection' expression: 'Array.Empty<int>()'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

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
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.IsEmpty failed. Expected collection of size 0. Actual: 1. 'collection' expression: 'collection'.");
    }

    public async Task NotAny_InterpolatedString_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsEmpty(collection, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).And
            .Message.Should().Be($"Assert.IsEmpty failed. Expected collection of size 0. Actual: 1. 'collection' expression: 'collection'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 0 element(s). 'collection' expression: 'Array.Empty<int>()'.");
    }

    public void Single_WhenMultipleItems_ShouldFail()
    {
        Action action = () => Assert.ContainsSingle([1, 2, 3]);
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 3 element(s). 'collection' expression: '[1, 2, 3]'.");
    }

    public async Task Single_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.ContainsSingle(Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).And
            .Message.Should().Be($"Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 0 element(s). 'collection' expression: 'Array.Empty<int>()'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public async Task Single_InterpolatedString_WhenMultipleItems_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.ContainsSingle([1, 2, 3], $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).And
            .Message.Should().Be($"Assert.ContainsSingle failed. Expected collection to contain exactly one element but found 3 element(s). 'collection' expression: '[1, 2, 3]'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected exactly one item to match the predicate but found 0 item(s). 'predicate' expression: 'x => x % 2 == 0', 'collection' expression: 'collection'.");
    }

    public void SinglePredicate_WhenMultipleItemsMatch_ShouldFail()
    {
        var collection = new List<int> { 2, 4, 6 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection);
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected exactly one item to match the predicate but found 3 item(s). 'predicate' expression: 'x => x % 2 == 0', 'collection' expression: 'collection'.");
    }

    public void SinglePredicate_Message_WhenNoItemMatches_ShouldFail()
    {
        var collection = new List<int> { 1, 3, 5 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, "No even numbers found: test");
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected exactly one item to match the predicate but found 0 item(s). 'predicate' expression: 'x => x % 2 == 0', 'collection' expression: 'collection'. No even numbers found: test");
    }

    public void SinglePredicate_Message_WhenMultipleItemsMatch_ShouldFail()
    {
        var collection = new List<int> { 2, 4, 6 };
        Action action = () => Assert.ContainsSingle(x => x % 2 == 0, collection, "Too many even numbers: test");
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.ContainsSingle failed. Expected exactly one item to match the predicate but found 3 item(s). 'predicate' expression: 'x => x % 2 == 0', 'collection' expression: 'collection'. Too many even numbers: test");
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
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. 'collection' expression: 'Array.Empty<int>()'.");
    }

    public async Task Any_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsNotEmpty(Array.Empty<string>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).And
            .Message.Should().Be($"Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. 'collection' expression: 'Array.Empty<string>()'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }
}
