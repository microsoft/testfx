// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    public void ExpectedExceptionAttributeConstructerShouldThrowArgumentNullExceptionWhenExceptionTypeIsNull()
    {
        static void A() => _ = new ExpectedExceptionAttribute(null, "Dummy");

        Exception ex = VerifyThrows(A);
        Verify(ex is ArgumentNullException);
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should throw ArgumentNullException when parameter exceptionType = typeof(AnyClassNotDerivedFromExceptionClass).
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldThrowArgumentException()
    {
        static void A() => _ = new ExpectedExceptionAttribute(typeof(ExpectedExceptionAttributeTests), "Dummy");

        Exception ex = VerifyThrows(A);
        Verify(ex is ArgumentException);
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should not throw exception when parameter exceptionType = typeof(AnyClassDerivedFromExceptionClass).
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldNotThrowAnyException() => _ = new ExpectedExceptionAttribute(typeof(DummyTestClassDerivedFromException), "Dummy");

    public void GetExceptionMsgShouldReturnExceptionMessage()
    {
        Exception ex = new("Dummy Exception");
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: Dummy Exception";
        Verify(expectedMessage == actualMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageAsWellIfPresent()
    {
        Exception innerException = new DivideByZeroException();
        Exception ex = new("Dummy Exception", innerException);
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: Dummy Exception ---> System.DivideByZeroException: Attempted to divide by zero.";
        Verify(expectedMessage == actualMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageRecursivelyIfPresent()
    {
        Exception recursiveInnerException = new IndexOutOfRangeException("ThirdLevelException");
        Exception innerException = new DivideByZeroException("SecondLevel Exception", recursiveInnerException);
        Exception ex = new("FirstLevelException", innerException);
        string actualMessage = UtfHelper.GetExceptionMsg(ex);
        string expectedMessage = "System.Exception: FirstLevelException ---> System.DivideByZeroException: SecondLevel Exception ---> System.IndexOutOfRangeException: ThirdLevelException";
        Verify(expectedMessage == actualMessage);
    }
}

/// <summary>
/// Dummy class derived from Exception.
/// </summary>
public class DummyTestClassDerivedFromException : Exception
{
}
