// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.RegularExpressions;

using global::TestFramework.ForTestingMSTest;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Assertions;
public class StringAssertTests : TestContainer
{
    public void ThatShouldReturnAnInstanceOfStringAssert()
    {
        Verify(StringAssert.That is not null);
    }

    public void ThatShouldCacheStringAssertInstance()
    {
        Verify(StringAssert.That == StringAssert.That);
    }

    public void StringAssertContains()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        var ex = VerifyThrows(() => StringAssert.Contains(actual, notInString));
        Verify(ex is not null);
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    public void StringAssertStartsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        var ex = VerifyThrows(() => StringAssert.StartsWith(actual, notInString));
        Verify(ex is not null);
        Verify(ex.Message.Contains("StringAssert.StartsWith failed"));
    }

    public void StringAssertEndsWith()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        string notInString = "I'm not in the string above";
        var ex = VerifyThrows(() => StringAssert.EndsWith(actual, notInString));
        Verify(ex is not null);
        Verify(ex.Message.Contains("StringAssert.EndsWith failed"));
    }

    public void StringAssertDoesNotMatch()
    {
        string actual = "The quick brown fox jumps over the lazy dog.";
        Regex doesMatch = new("quick brown fox");
        var ex = VerifyThrows(() => StringAssert.DoesNotMatch(actual, doesMatch));
        Verify(ex is not null);
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
        var ex = VerifyThrows(() => StringAssert.Contains(":-{", "x"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsDoesNotThrowFormatExceptionWithArguments()
    {
        var ex = VerifyThrows(() => StringAssert.Contains("{", "x", "message {0}", "arg"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("StringAssert.Contains failed"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void StringAssertContainsFailsIfMessageIsInvalidStringFormatComposite()
    {
        var ex = VerifyThrows(() => StringAssert.Contains("a", "b", "message {{0}", "arg"));
        Verify(ex is not null);
        Verify(ex is FormatException);
    }
}
