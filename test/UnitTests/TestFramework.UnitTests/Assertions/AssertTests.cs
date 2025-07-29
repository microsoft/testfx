// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region Instance tests
    public void InstanceShouldReturnAnInstanceOfAssert() => Assert.Instance.Should().NotBeNull();

    public void InstanceShouldCacheAssertInstance() => ReferenceEquals(Assert.Instance, Assert.Instance));
    #endregion

    #region ReplaceNullChars tests
    public void ReplaceNullCharsShouldReturnStringIfNullOrEmpty()
    {
        Verify(Assert.ReplaceNullChars(null).Should().Be(null);
        Assert.ReplaceNullChars(string.Empty).Should().Be(string.Empty);
    }

    public void ReplaceNullCharsShouldReplaceNullCharsInAString() => Assert.ReplaceNullChars("The quick brown fox \0 jumped over the la\0zy dog\0").Should().Be("The quick brown fox \\0 jumped over the la\\0zy dog\\0");
    #endregion

    #region BuildUserMessage tests

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageThrowsWhenMessageContainsInvalidStringFormatComposite()
    {
        Exception ex = VerifyThrows(() => Assert.BuildUserMessage("{", "arg"));
        ex is FormatException.Should().BeTrue();
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        string message = Assert.BuildUserMessage("{");
        message.Should().Be("{");
    }
    #endregion

    #region Obsolete methods tests
#if DEBUG
    public void ObsoleteEqualsMethodThrowsAssertFailedException()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Exception ex = VerifyThrows(() => Assert.Equals("test", "test"));
#pragma warning restore CS0618 // Type or member is obsolete
        ex is AssertFailedException.Should().BeTrue();
        Verify(ex.Message.Contains("Assert.Equals should not be used for Assertions"));
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Exception ex = VerifyThrows(() => Assert.ReferenceEquals(obj, obj));
#pragma warning restore CS0618 // Type or member is obsolete
        ex is AssertFailedException.Should().BeTrue();
        Verify(ex.Message.Contains("Assert.ReferenceEquals should not be used for Assertions"));
    }
#endif
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
