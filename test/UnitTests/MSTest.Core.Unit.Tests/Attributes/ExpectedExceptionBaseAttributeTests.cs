// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

/// <summary>
/// Tests for class ExpectedExceptionBaseAttribute
/// </summary>
public class ExpectedExceptionBaseAttributeTests : TestContainer
{
    private TestableExpectedExceptionBaseAttributeClass _sut = null;

    /// <summary>
    /// Test initialization function.
    /// </summary>
    public ExpectedExceptionBaseAttributeTests()
    {
        _sut = new TestableExpectedExceptionBaseAttributeClass();
    }

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertFailedException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertFailure()
    {
        void a() => _sut.RethrowIfAssertException(new AssertFailedException());

        var ex = VerifyThrows(a);
        Verify(ex.GetType() == typeof(AssertFailedException));
    }

    /// <summary>
    /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertInconclusiveException as parameter in it.
    /// </summary>
    public void RethrowIfAssertExceptionThrowsExceptionOnAssertInconclusive()
    {
        void a() => _sut.RethrowIfAssertException(new AssertInconclusiveException());

        var ex = VerifyThrows(a);
        Verify(ex.GetType() == typeof(AssertInconclusiveException));
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
/// Dummy class derived from Exception
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

    public string GetNoExceptionMessage()
    {
        return SpecifiedNoExceptionMessage;
    }

    /// <summary>
    /// Re-throw the exception if it is an AssertFailedException or an AssertInconclusiveException
    /// </summary>
    /// <param name="exception">The exception to re-throw if it is an assertion exception</param>
    public new void RethrowIfAssertException(Exception exception)
    {
        base.RethrowIfAssertException(exception);
    }

    protected internal override void Verify(Exception exception)
    {
        return;
    }
}
