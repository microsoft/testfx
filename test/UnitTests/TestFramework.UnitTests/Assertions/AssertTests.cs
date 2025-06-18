﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region Instance tests
    public void InstanceShouldReturnAnInstanceOfAssert() => Verify(Assert.Instance is not null);

    public void InstanceShouldCacheAssertInstance() => Verify(ReferenceEquals(Assert.Instance, Assert.Instance));
    #endregion

    #region ReplaceNullChars tests
    public void ReplaceNullCharsShouldReturnStringIfNullOrEmpty()
    {
        Verify(Assert.ReplaceNullChars(null) == null);
        Verify(Assert.ReplaceNullChars(string.Empty) == string.Empty);
    }

    public void ReplaceNullCharsShouldReplaceNullCharsInAString() => Verify(Assert.ReplaceNullChars("The quick brown fox \0 jumped over the la\0zy dog\0") == "The quick brown fox \\0 jumped over the la\\0zy dog\\0");
    #endregion

    #region BuildUserMessage tests

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageThrowsWhenMessageContainsInvalidStringFormatComposite()
    {
        Exception ex = VerifyThrows(() => Assert.BuildUserMessage("{", "arg"));
        Verify(ex is FormatException);
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        string message = Assert.BuildUserMessage("{");
        Verify(message == "{");
    }
    #endregion

    private static Task<string> GetHelloStringAsync()
        => Task.FromResult("Hello");

    private sealed class DummyClassTrackingToStringCalls
    {
        public bool WasToStringCalled { get; private set; }

        public override string ToString()
        {
            WasToStringCalled = true;
            return nameof(DummyClassTrackingToStringCalls);
        }
    }

    private sealed class DummyIFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider)
            => "DummyIFormattable.ToString()";
    }
}
