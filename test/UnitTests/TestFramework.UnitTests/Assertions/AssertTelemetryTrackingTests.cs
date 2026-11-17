// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using AwesomeAssertions;
using AwesomeAssertions.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Every public assertion must report exactly one usage to the telemetry collector per call: a missing call hides the
/// API from the usage statistics, and a duplicated call (for example, a public overload and the shared helper it
/// delegates to both tracking) inflates them. <see cref="TelemetryTestInitializer"/> makes the counters observable
/// from this process. Test methods that exercise several overloads wrap them in an <see cref="AssertionScope"/>, so
/// one failing overload does not hide the others.
/// </summary>
public sealed partial class AssertTelemetryTrackingTests : TestContainer
{
    private static void AssertTracked(string expectedName, Action call, [CallerArgumentExpression(nameof(call))] string callExpression = "")
    {
        _ = TelemetryCollector.DrainAssertionCallCounts();

        call.Should().NotThrow("{0}", callExpression);

        AssertCounts(expectedName, callExpression);
    }

    private static void AssertTrackedWhenFailing<TException>(string expectedName, Action call, [CallerArgumentExpression(nameof(call))] string callExpression = "")
        where TException : Exception
    {
        _ = TelemetryCollector.DrainAssertionCallCounts();

        call.Should().Throw<TException>("{0}", callExpression);

        AssertCounts(expectedName, callExpression);
    }

    private static async Task AssertTrackedAsync(string expectedName, Func<Task> call, [CallerArgumentExpression(nameof(call))] string callExpression = "")
    {
        _ = TelemetryCollector.DrainAssertionCallCounts();

        await call.Should().NotThrowAsync("{0}", callExpression);

        AssertCounts(expectedName, callExpression);
    }

    private static void AssertCounts(string expectedName, string callExpression)
    {
        Dictionary<string, long> counts = TelemetryCollector.DrainAssertionCallCounts();
        counts.Should().BeEquivalentTo(new Dictionary<string, long> { [expectedName] = 1 }, "{0}", callExpression);
    }

    public void Fail_IsTracked()
        => AssertTrackedWhenFailing<AssertFailedException>("Assert.Fail", () => Assert.Fail("message"));

    public void Inconclusive_IsTracked()
        => AssertTrackedWhenFailing<AssertInconclusiveException>("Assert.Inconclusive", () => Assert.Inconclusive("message"));

    public void AreEqual_IsTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1, 1));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1, 1, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1, 1, $"message {suffix}"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1, 1, EqualityComparer<int>.Default, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1, 1, EqualityComparer<int>.Default, $"message {suffix}"));
    }

    public void AreNotEqual_IsTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1, 2));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1, 2, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1, 2, $"message {suffix}"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1, 2, EqualityComparer<int>.Default, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1, 2, EqualityComparer<int>.Default, $"message {suffix}"));
    }

    public void AreEqual_Numerics_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1.0, 1.0, 0.0, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1.0, 1.0, 0.0, $"message {suffix}"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1f, 1f, 0f, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1f, 1f, 0f, $"message {suffix}"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1m, 1m, 0m, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1m, 1m, 0m, $"message {suffix}"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1L, 1L, 0L, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual(1L, 1L, 0L, $"message {suffix}"));
    }

    public void AreNotEqual_Numerics_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1.0, 3.0, 1.0, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1.0, 3.0, 1.0, $"message {suffix}"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1f, 3f, 1f, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1f, 3f, 1f, $"message {suffix}"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1m, 3m, 1m, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1m, 3m, 1m, $"message {suffix}"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1L, 3L, 1L, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual(1L, 3L, 1L, $"message {suffix}"));
    }

    public void AreEqual_Strings_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual("a", "A", ignoreCase: true, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual("a", "A", ignoreCase: true, $"message {suffix}"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual("a", "A", ignoreCase: true, CultureInfo.InvariantCulture, "message"));
        AssertTracked("Assert.AreEqual", () => Assert.AreEqual("a", "A", ignoreCase: true, CultureInfo.InvariantCulture, $"message {suffix}"));
    }

    public void AreNotEqual_Strings_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual("a", "b", ignoreCase: true, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual("a", "b", ignoreCase: true, $"message {suffix}"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual("a", "b", ignoreCase: true, CultureInfo.InvariantCulture, "message"));
        AssertTracked("Assert.AreNotEqual", () => Assert.AreNotEqual("a", "b", ignoreCase: true, CultureInfo.InvariantCulture, $"message {suffix}"));
    }

    public void AreSame_IsTracked()
    {
        using var assertions = new AssertionScope();
        object instance = new();
        int suffix = 1;
        AssertTracked("Assert.AreSame", () => Assert.AreSame(instance, instance, "message"));
        AssertTracked("Assert.AreSame", () => Assert.AreSame(instance, instance, $"message {suffix}"));
        AssertTracked("Assert.AreNotSame", () => Assert.AreNotSame(instance, new object(), "message"));
        AssertTracked("Assert.AreNotSame", () => Assert.AreNotSame(instance, new object(), $"message {suffix}"));
    }

    public void IsNull_IsNotNull_AreTracked()
    {
        using var assertions = new AssertionScope();
        object instance = new();
        int suffix = 1;
        AssertTracked("Assert.IsNull", () => Assert.IsNull(null, "message"));
        AssertTracked("Assert.IsNull", () => Assert.IsNull(null, $"message {suffix}"));
        AssertTracked("Assert.IsNotNull", () => Assert.IsNotNull(instance, "message"));
        AssertTracked("Assert.IsNotNull", () => Assert.IsNotNull(instance, $"message {suffix}"));
    }

    public void IsTrue_IsFalse_AreTracked()
    {
        using var assertions = new AssertionScope();
        int suffix = 1;
        AssertTracked("Assert.IsTrue", () => Assert.IsTrue(true, "message"));
        AssertTracked("Assert.IsTrue", () => Assert.IsTrue(true, $"message {suffix}"));
        AssertTracked("Assert.IsFalse", () => Assert.IsFalse(false, "message"));
        AssertTracked("Assert.IsFalse", () => Assert.IsFalse(false, $"message {suffix}"));
    }

    public void Comparisons_AreTracked()
    {
        using var assertions = new AssertionScope();
        AssertTracked("Assert.IsGreaterThan", () => Assert.IsGreaterThan(1, 2, "message"));
        AssertTracked("Assert.IsGreaterThanOrEqualTo", () => Assert.IsGreaterThanOrEqualTo(1, 1, "message"));
        AssertTracked("Assert.IsLessThan", () => Assert.IsLessThan(2, 1, "message"));
        AssertTracked("Assert.IsLessThanOrEqualTo", () => Assert.IsLessThanOrEqualTo(1, 1, "message"));
        AssertTracked("Assert.IsPositive", () => Assert.IsPositive(1, "message"));
        AssertTracked("Assert.IsNegative", () => Assert.IsNegative(-1, "message"));
        AssertTracked("Assert.IsInRange", () => Assert.IsInRange(1, 3, 2, "message"));
    }

    public void IsInstanceOfType_IsTracked()
    {
        using var assertions = new AssertionScope();
        object text = "text";
        int suffix = 1;
        AssertTracked("Assert.IsInstanceOfType", () => Assert.IsInstanceOfType(text, typeof(string), "message"));
        AssertTracked("Assert.IsInstanceOfType", () => Assert.IsInstanceOfType(text, typeof(string), $"message {suffix}"));
        AssertTracked("Assert.IsInstanceOfType", () => Assert.IsInstanceOfType<string>(text, "message"));
        AssertTracked("Assert.IsInstanceOfType", () => Assert.IsInstanceOfType<string>(text, $"message {suffix}"));
        AssertTracked("Assert.IsNotInstanceOfType", () => Assert.IsNotInstanceOfType(text, typeof(int), "message"));
        AssertTracked("Assert.IsNotInstanceOfType", () => Assert.IsNotInstanceOfType(text, typeof(int), $"message {suffix}"));
        AssertTracked("Assert.IsNotInstanceOfType", () => Assert.IsNotInstanceOfType<int>(text, "message"));
        AssertTracked("Assert.IsNotInstanceOfType", () => Assert.IsNotInstanceOfType<int>(text, $"message {suffix}"));
    }

    public void IsExactInstanceOfType_IsTracked()
    {
        using var assertions = new AssertionScope();
        object text = "text";
        int suffix = 1;
        AssertTracked("Assert.IsExactInstanceOfType", () => Assert.IsExactInstanceOfType(text, typeof(string), "message"));
        AssertTracked("Assert.IsExactInstanceOfType", () => Assert.IsExactInstanceOfType(text, typeof(string), $"message {suffix}"));
        AssertTracked("Assert.IsExactInstanceOfType", () => Assert.IsExactInstanceOfType<string>(text, "message"));
        AssertTracked("Assert.IsExactInstanceOfType", () => Assert.IsExactInstanceOfType<string>(text, $"message {suffix}"));
        AssertTracked("Assert.IsNotExactInstanceOfType", () => Assert.IsNotExactInstanceOfType(text, typeof(int), "message"));
        AssertTracked("Assert.IsNotExactInstanceOfType", () => Assert.IsNotExactInstanceOfType(text, typeof(int), $"message {suffix}"));
        AssertTracked("Assert.IsNotExactInstanceOfType", () => Assert.IsNotExactInstanceOfType<int>(text, "message"));
        AssertTracked("Assert.IsNotExactInstanceOfType", () => Assert.IsNotExactInstanceOfType<int>(text, $"message {suffix}"));
    }

    public void MatchesRegex_IsTracked()
    {
        using var assertions = new AssertionScope();
        AssertTracked("Assert.MatchesRegex", () => Assert.MatchesRegex(new Regex("a+"), "aaa", "message"));
        AssertTracked("Assert.MatchesRegex", () => Assert.MatchesRegex("a+", "aaa", "message"));
        AssertTracked("Assert.DoesNotMatchRegex", () => Assert.DoesNotMatchRegex(new Regex("a+"), "bbb", "message"));
        AssertTracked("Assert.DoesNotMatchRegex", () => Assert.DoesNotMatchRegex("a+", "bbb", "message"));
    }

    public void StartsWith_EndsWith_AreTracked()
    {
        using var assertions = new AssertionScope();
        AssertTracked("Assert.StartsWith", () => Assert.StartsWith("ab", "abc", "message"));
        AssertTracked("Assert.StartsWith", () => Assert.StartsWith("ab", "abc", StringComparison.Ordinal, "message"));
        AssertTracked("Assert.DoesNotStartWith", () => Assert.DoesNotStartWith("x", "abc", "message"));
        AssertTracked("Assert.DoesNotStartWith", () => Assert.DoesNotStartWith("x", "abc", StringComparison.Ordinal, "message"));
        AssertTracked("Assert.EndsWith", () => Assert.EndsWith("bc", "abc", "message"));
        AssertTracked("Assert.EndsWith", () => Assert.EndsWith("bc", "abc", StringComparison.Ordinal, "message"));
        AssertTracked("Assert.DoesNotEndWith", () => Assert.DoesNotEndWith("x", "abc", "message"));
        AssertTracked("Assert.DoesNotEndWith", () => Assert.DoesNotEndWith("x", "abc", StringComparison.Ordinal, "message"));
    }

    public void That_IsTracked()
    {
        using var assertions = new AssertionScope();
        AssertTracked("Assert.That", () => Assert.That(() => true));
        AssertTracked("Assert.That", () => Assert.That(() => true, "message"));
    }

    public void Throws_IsTracked()
    {
        using var assertions = new AssertionScope();
        Action action = static () => throw new InvalidOperationException();
        Func<object?> function = static () => throw new InvalidOperationException();
        int suffix = 1;
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(action, "message"));
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(function, "message"));
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(action, _ => "message"));
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(function, _ => "message"));
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(action, $"message {suffix}"));
        AssertTracked("Assert.Throws", () => Assert.Throws<InvalidOperationException>(function, $"message {suffix}"));
    }

    public void ThrowsExactly_IsTracked()
    {
        using var assertions = new AssertionScope();
        Action action = static () => throw new InvalidOperationException();
        Func<object?> function = static () => throw new InvalidOperationException();
        int suffix = 1;
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(action, "message"));
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(function, "message"));
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(action, _ => "message"));
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(function, _ => "message"));
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(action, $"message {suffix}"));
        AssertTracked("Assert.ThrowsExactly", () => Assert.ThrowsExactly<InvalidOperationException>(function, $"message {suffix}"));
    }

    public async Task ThrowsAsync_IsTracked()
    {
        using var assertions = new AssertionScope();
        Func<Task> action = static () => Task.FromException(new InvalidOperationException());
        int suffix = 1;
        await AssertTrackedAsync("Assert.ThrowsAsync", () => Assert.ThrowsAsync<InvalidOperationException>(action, "message"));
        await AssertTrackedAsync("Assert.ThrowsAsync", () => Assert.ThrowsAsync<InvalidOperationException>(action, _ => "message"));
        await AssertTrackedAsync("Assert.ThrowsAsync", () => Assert.ThrowsAsync<InvalidOperationException>(action, $"message {suffix}"));
    }

    public async Task ThrowsExactlyAsync_IsTracked()
    {
        using var assertions = new AssertionScope();
        Func<Task> action = static () => Task.FromException(new InvalidOperationException());
        int suffix = 1;
        await AssertTrackedAsync("Assert.ThrowsExactlyAsync", () => Assert.ThrowsExactlyAsync<InvalidOperationException>(action, "message"));
        await AssertTrackedAsync("Assert.ThrowsExactlyAsync", () => Assert.ThrowsExactlyAsync<InvalidOperationException>(action, _ => "message"));
        await AssertTrackedAsync("Assert.ThrowsExactlyAsync", () => Assert.ThrowsExactlyAsync<InvalidOperationException>(action, $"message {suffix}"));
    }
}
