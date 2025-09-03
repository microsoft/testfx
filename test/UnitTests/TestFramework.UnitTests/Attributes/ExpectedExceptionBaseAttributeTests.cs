// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ExpectedExceptionBaseAttribute.
/// </summary>
public class ExpectedExceptionBaseAttributeTests : TestContainer
{
    private TestableExpectedExceptionBaseAttributeClass _sut;

    public ExpectedExceptionBaseAttributeTests() => _sut = new TestableExpectedExceptionBaseAttributeClass();

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertFailedException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertFailure()
    {
        Action action = () => _sut.RethrowIfAssertException(new AssertFailedException());
        action.Should().Throw<AssertFailedException>();
    }

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertInconclusiveException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertInconclusive()
    {
        Action action = () => _sut.RethrowIfAssertException(new AssertInconclusiveException());
        action.Should().Throw<AssertInconclusiveException>();
    }

    public void VerifyCorrectMessageIsGettingSetInVariableNoExceptionMessage()
    {
        string expected = "DummyString";
        _sut = new TestableExpectedExceptionBaseAttributeClass(expected);

        string result = _sut.GetNoExceptionMessage();

        result.Should().Be(expected);
    }

    public void VerifyEmptyMessageIsGettingSetInVariableNoExceptionMessage()
    {
        _sut = new TestableExpectedExceptionBaseAttributeClass(null!);

        string result = _sut.GetNoExceptionMessage();

        result.Should().BeNullOrEmpty();
    }
}

/// <summary>
/// Dummy class derived from Exception.
/// </summary>
public class TestableExpectedExceptionBaseAttributeClass : ExpectedExceptionBaseAttribute
{
    public TestableExpectedExceptionBaseAttributeClass()
        : base()
    {
    }

    public TestableExpectedExceptionBaseAttributeClass(string noExceptionMessage)
        : base(noExceptionMessage)
    {
    }

    public string GetNoExceptionMessage() => SpecifiedNoExceptionMessage;

    /// <summary>
    /// Re-throw the exception if it is an AssertFailedException or an AssertInconclusiveException.
    /// </summary>
    /// <param name="exception">The exception to re-throw if it is an assertion exception.</param>
    public new void RethrowIfAssertException(Exception exception) => base.RethrowIfAssertException(exception);

    protected internal override void Verify(Exception exception)
    {
    }
}
