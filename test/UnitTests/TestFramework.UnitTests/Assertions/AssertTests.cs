// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region Instance tests
    public void InstanceShouldReturnAnInstanceOfAssert() => Assert.That.Should().NotBeNull();

    public void InstanceShouldCacheAssertInstance() => Assert.That.Should().BeSameAs(Assert.That);
    #endregion

    #region ReplaceNullChars tests
    public void ReplaceNullCharsShouldReturnStringIfNullOrEmpty()
    {
        Assert.ReplaceNullChars(null).Should().BeNull();
        Assert.ReplaceNullChars(string.Empty).Should().BeSameAs(string.Empty);
    }

    public void ReplaceNullCharsShouldReplaceNullCharsInAString() => Assert.ReplaceNullChars("The quick brown fox \0 jumped over the la\0zy dog\0").Should().Be("The quick brown fox \\0 jumped over the la\\0zy dog\\0");
    #endregion

    #region BuildUserMessage tests

    // See https://github.com/dotnet/sdk/issues/25373
    public void BuildUserMessageThrowsWhenMessageContainsInvalidStringFormatComposite()
    {
        Action act = () => Assert.BuildUserMessage("{", "arg");
        act.Should().Throw<FormatException>();
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
        Action act = () => Assert.Equals("test", "test");
#pragma warning restore CS0618 // Type or member is obsolete
        act.Should().Throw<AssertFailedException>()
           .WithMessage("*Assert.Equals should not be used for Assertions*");
    }

    public void ObsoleteReferenceEqualsMethodThrowsAssertFailedException()
    {
        object obj = new();
#pragma warning disable CS0618 // Type or member is obsolete
        Action act = () => Assert.ReferenceEquals(obj, obj);
#pragma warning restore CS0618 // Type or member is obsolete
        act.Should().Throw<AssertFailedException>()
           .WithMessage("*Assert.ReferenceEquals should not be used for Assertions*");
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
