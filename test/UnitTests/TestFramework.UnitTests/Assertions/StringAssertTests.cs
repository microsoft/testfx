// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Text.RegularExpressions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;

public class StringAssertTests : TestContainer
{
    public void ThatShouldReturnAnInstanceOfStringAssert() => Verify(StringAssert.That is not null);

    public void ThatShouldCacheStringAssertInstance() => Verify(StringAssert.That == StringAssert.That);

    public void StringAssertContains()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Exception ex = VerifyThrows(() => StringAssert.Contains(actual, notInString));
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    public void StringAssertStartsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Exception ex = VerifyThrows(() => StringAssert.StartsWith(actual, notInString));
        Verify(ex.Message.Contains("StringAssert.StartsWith failed"));
    }

    public void StringAssertEndsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        Exception ex = VerifyThrows(() => StringAssert.EndsWith(actual, notInString));
        Verify(ex.Message.Contains("StringAssert.EndsWith failed"));
    }

    public void StringAssertDoesNotMatch()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        Regex doesMatch = new("quick brown fox");
        Exception ex = VerifyThrows(() => StringAssert.DoesNotMatch(actual, doesMatch));
        Verify(ex.Message.Contains("StringAssert.DoesNotMatch failed"));
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
        Exception ex = VerifyThrows(() => StringAssert.Contains(":-{", "x"));
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsDoesNotThrowFormatExceptionWithArguments()
    {
        Exception ex = VerifyThrows(() => StringAssert.Contains("{", "x", "message {0}", "arg"));
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsFailsIfMessageIsInvalidStringFormatComposite()
    {
        Exception ex = VerifyThrows(() => StringAssert.Contains("a", "b", "message {{0}", "arg"));
        Verify(ex is FormatException);
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
        StringAssert.Contains(value, substring, "message", StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertContainsMessageParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring, "message format {0} {1}", 1, 2);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertContainsMessageStringComparisonParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.Contains(value, substring, "message format {0} {1}", StringComparison.OrdinalIgnoreCase, 1, 2);
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
        StringAssert.StartsWith(value, substring, "message", StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithMessageParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring, "message format {0} {1}", 1, 2);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertStartsWithMessageStringComparisonParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingStartsWithString();
        StringAssert.StartsWith(value, substring, "message format {0} {1}", StringComparison.OrdinalIgnoreCase, 1, 2);
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
        StringAssert.EndsWith(value, substring, "message", StringComparison.OrdinalIgnoreCase);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithMessageParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring, "message format {0} {1}", 1, 2);
        value.ToString(); // no warning
        substring.ToString(); // no warning
    }

    public void StringAssertEndsWithMessageStringComparisonParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        string? substring = GetMatchingEndsWithString();
        StringAssert.EndsWith(value, substring, "message format {0} {1}", StringComparison.OrdinalIgnoreCase, 1, 2);
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

    public void StringAssertMatchesMessageParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetMatchingPattern();
        StringAssert.Matches(value, pattern, "message format {0} {1}", 1, 2);
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

    public void StringAssertDoesNotMatchMessageParametersNullabilitiesPostConditions()
    {
        string? value = GetValue();
        Regex? pattern = GetNonMatchingPattern();
        StringAssert.DoesNotMatch(value, pattern, "message format {0} {1}", 1, 2);
        value.ToString(); // no warning
        pattern.ToString(); // no warning
    }

    private string? GetValue() => "some value";

    private string? GetMatchingStartsWithString() => "some";

    private string? GetMatchingEndsWithString() => "value";

    private Regex? GetMatchingPattern() => new("some*");

    private Regex? GetNonMatchingPattern() => new("something");
}
