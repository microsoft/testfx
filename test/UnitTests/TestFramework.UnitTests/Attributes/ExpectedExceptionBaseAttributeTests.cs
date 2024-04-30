// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ExpectedExceptionBaseAttribute.
/// </summary>
public class ExpectedExceptionBaseAttributeTests : TestContainer
{
    private TestableExpectedExceptionBaseAttributeClass _sut;

    public ExpectedExceptionBaseAttributeTests()
    {
        _sut = new TestableExpectedExceptionBaseAttributeClass();
    }

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertFailedException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertFailure()
    {
        void A() => _sut.RethrowIfAssertException(new AssertFailedException());

        Exception ex = VerifyThrows(A);
        Verify(ex is AssertFailedException);
    }

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertInconclusiveException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertInconclusive()
    {
        void A() => _sut.RethrowIfAssertException(new AssertInconclusiveException());

        Exception ex = VerifyThrows(A);
        Verify(ex is AssertInconclusiveException);
    }

    public void VerifyCorrectMessageIsGettingSetInVariablenoExceptionMessage()
    {
        string expected = "DummyString";
        _sut = new TestableExpectedExceptionBaseAttributeClass(expected);

        string result = _sut.GetNoExceptionMessage();

        Verify(expected == result);
    }

    public void VerifyEmptyMessageIsGettingSetInVariablenoExceptionMessage()
    {
        _sut = new TestableExpectedExceptionBaseAttributeClass(null);

        string result = _sut.GetNoExceptionMessage();

        Verify(string.IsNullOrEmpty(result));
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
        return;
    }
}
