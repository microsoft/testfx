// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public class StringAssertTests : TestContainer
{
    public void InstanceShouldReturnAnInstanceOfStringAssert() => StringAssert.That.Should().NotBeNull();

    public void InstanceShouldCacheStringAssertInstance() => StringAssert.That.Should().BeSameAs(StringAssert.That);

    public void StringAssertContains()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Action action = () => StringAssert.Contains(actual, notInString);
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.Contains failed");
    }

    public void StringAssertStartsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Action action = () => StringAssert.StartsWith(actual, notInString);
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.StartsWith failed");
    }

    public void StringAssertEndsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Action action = () => StringAssert.EndsWith(actual, notInString);
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.EndsWith failed");
    }

    public void StringAssertDoesNotMatch()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        Regex doesMatch = new("quick brown fox");
        Action action = () => StringAssert.DoesNotMatch(actual, doesMatch);
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.DoesNotMatch failed");
    }

    public void StringAssertContainsIgnoreCase_DoesNotThrow()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string inString = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG.";
        StringAssert.Contains(actual, inString, StringComparison.OrdinalIgnoreCase);
    }

    public void StringAssertStartsWithIgnoreCase_DoesNotThrow()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string inString = "THE QUICK";
        StringAssert.StartsWith(actual, inString, StringComparison.OrdinalIgnoreCase);
    }

    public void StringAssertEndsWithIgnoreCase_DoesNotThrow()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string inString = "LAZY DOG.";
        StringAssert.EndsWith(actual, inString, StringComparison.OrdinalIgnoreCase);
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsDoesNotThrowFormatException()
    {
        Action action = () => StringAssert.Contains(":-{", "x");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.Contains failed");
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsDoesNotThrowFormatExceptionWithArguments()
    {
        Action action = () => StringAssert.Contains("{", "x", "message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("StringAssert.Contains failed");
    }

    public void StringAssertContainsNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertContainsStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring, StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertContainsMessageNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertContainsMessageStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring, StringComparison.OrdinalIgnoreCase, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring, StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithMessageNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithMessageStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring, StringComparison.OrdinalIgnoreCase, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring, StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithMessageNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithMessageStringComparisonNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring, StringComparison.OrdinalIgnoreCase, "message");
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertMatchesNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetMatchingPattern();
        StringAssert.Matches(value, pattern);
        value.ToString(); // no warning
        pattern.ToString(); // no warning
    }

    public void StringAssertMatchesMessageNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetMatchingPattern();
        StringAssert.Matches(value, pattern, "message");
        value.ToString(); // no warning
        pattern.ToString(); // no warning
    }

    public void StringAssertDoesNotMatchNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetNonMatchingPattern();
        StringAssert.DoesNotMatch(value, pattern);
        value.ToString(); // no warning
        pattern.ToString(); // no warning
    }

    public void StringAssertDoesNotMatchMessageNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetNonMatchingPattern();
        StringAssert.DoesNotMatch(value, pattern, "message");
        value.ToString(); // no warning
        pattern.ToString(); // no warning
    }

    private string? GetValue() => "some value";

    private string? GetMatchingStartsWithString() => "some";

    private string? GetMatchingEndsWithString() => "value";

    private Regex? GetMatchingPattern() => new("some*");

    private Regex? GetNonMatchingPattern() => new("something");

    #region Obsolete methods tests
#if DEBUG
    public void ObsoleteEqualsMethodThrowsAssertFailedException()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Action action = () => StringAssert.Equals("test", "test");
#pragma warning restore CS0618 // Type or member is obsolete
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("StringAssert.Equals should not be used for Assertions");
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Action action = () => StringAssert.ReferenceEquals(obj, obj);
#pragma warning restore CS0618 // Type or member is obsolete
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("StringAssert.ReferenceEquals should not be used for Assertions");
    }
#endif
    #endregion
}
