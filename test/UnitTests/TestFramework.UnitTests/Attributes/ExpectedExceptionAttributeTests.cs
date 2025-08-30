// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class ExpectedExceptionAttribute.
/// </summary>
public class ExpectedExceptionAttributeTests : TestContainer
{
    /// <summary>
    /// ExpectedExceptionAttribute constructor should throw ArgumentNullException when parameter exceptionType = null.
    /// </summary>
    public void ExpectedExceptionAttributeConstructorShouldThrowArgumentNullExceptionWhenExceptionTypeIsNull()
    {
        Action action = () => _ = new ExpectedExceptionAttribute(null!, "Dummy");

        action.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should throw ArgumentNullException when parameter exceptionType = typeof(AnyClassNotDerivedFromExceptionClass).
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldThrowArgumentException()
    {
        Action action = () => _ = new ExpectedExceptionAttribute(typeof(ExpectedExceptionAttributeTests), "Dummy");
        action.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should not throw exception when parameter exceptionType = typeof(AnyClassDerivedFromExceptionClass).
    /// </summary>
    public void ExpectedExceptionAttributeConstructorShouldNotThrowAnyException()
        => _ = new ExpectedExceptionAttribute(typeof(DummyTestClassDerivedFromException), "Dummy");

    public void GetExceptionMsgShouldReturnExceptionMessage()
    {
        Exception ex = new("Dummy Exception");
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: Dummy Exception";
        actualMessage.Should().Be(expectedMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageAsWellIfPresent()
    {
        Exception innerException = new DivideByZeroException();
        Exception ex = new("Dummy Exception", innerException);
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: Dummy Exception ---> System.DivideByZeroException: Attempted to divide by zero.";
        actualMessage.Should().Be(expectedMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageRecursivelyIfPresent()
    {
        Exception recursiveInnerException = new IndexOutOfRangeException("ThirdLevelException");
        Exception innerException = new DivideByZeroException("SecondLevel Exception", recursiveInnerException);
        Exception ex = new("FirstLevelException", innerException);
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: FirstLevelException ---> System.DivideByZeroException: SecondLevel Exception ---> System.IndexOutOfRangeException: ThirdLevelException";
        actualMessage.Should().Be(expectedMessage);
    }
}

/// <summary>
/// Dummy class derived from Exception.
/// </summary>
public class DummyTestClassDerivedFromException : Exception;
