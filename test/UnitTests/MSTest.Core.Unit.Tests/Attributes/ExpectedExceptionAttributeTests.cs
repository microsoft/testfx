// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

/// <summary>
/// Tests for class ExpectedExceptionAttribute
/// </summary>
public class ExpectedExceptionAttributeTests : TestContainer
{
    /// <summary>
    /// ExpectedExceptionAttribute constructor should throw ArgumentNullException when parameter exceptionType = null
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldThrowArgumentNullExceptionWhenExceptionTypeIsNull()
    {
        static void a() => _ = new ExpectedExceptionAttribute(null, "Dummy");

        var ex = VerifyThrows(a);
        Verify(ex is ArgumentNullException);
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should throw ArgumentNullException when parameter exceptionType = typeof(AnyClassNotDerivedFromExceptionClass)
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldThrowArgumentException()
    {
        static void a() => _ = new ExpectedExceptionAttribute(typeof(ExpectedExceptionAttributeTests), "Dummy");

        var ex = VerifyThrows(a);
        Verify(ex.GetType() == typeof(ArgumentException));
    }

    /// <summary>
    /// ExpectedExceptionAttribute constructor should not throw exception when parameter exceptionType = typeof(AnyClassDerivedFromExceptionClass)
    /// </summary>
    public void ExpectedExceptionAttributeConstructerShouldNotThrowAnyException()
    {
        ExpectedExceptionAttribute sut = new(typeof(DummyTestClassDerivedFromException), "Dummy");
    }

    public void GetExceptionMsgShouldReturnExceptionMessage()
    {
        Exception ex = new("Dummy Exception");
        var actualMessage = UtfHelper.GetExceptionMsg(ex);
        var expectedMessage = "System.Exception: Dummy Exception";
        Verify(expectedMessage == actualMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageAsWellIfPresent()
    {
        Exception innerException = new DivideByZeroException();
        Exception ex = new("Dummy Exception", innerException);
        var actualMessage = UtfHelper.GetExceptionMsg(ex);
        var expectedMessage = "System.Exception: Dummy Exception ---> System.DivideByZeroException: Attempted to divide by zero.";
        Verify(expectedMessage == actualMessage);
    }

    public void GetExceptionMsgShouldReturnInnerExceptionMessageRecursivelyIfPresent()
    {
        Exception recursiveInnerException = new IndexOutOfRangeException("ThirdLevelException");
        Exception innerException = new DivideByZeroException("SecondLevel Exception", recursiveInnerException);
        Exception ex = new("FirstLevelException", innerException);
        var actualMessage = UtfHelper.GetExceptionMsg(ex);
        var expectedMessage = "System.Exception: FirstLevelException ---> System.DivideByZeroException: SecondLevel Exception ---> System.IndexOutOfRangeException: ThirdLevelException";
        Verify(expectedMessage == actualMessage);
    }
}

/// <summary>
/// Dummy class derived from Exception
/// </summary>
public class DummyTestClassDerivedFromException : Exception
{
}
