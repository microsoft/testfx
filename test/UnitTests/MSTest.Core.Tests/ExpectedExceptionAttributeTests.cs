// Copyright (c) Microsoft. All rights reserved.

namespace UnitTestFramework.Tests
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System;

    using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;
    using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
    using MSTestAdapter.TestUtilities;

    /// <summary>
    /// Tests for class ExpectedExceptionAttribute
    /// </summary>
    [TestFrameworkV1.TestClass]
    public class ExpectedExceptionAttributeTests
    {
        /// <summary>
        /// ExpectedExceptionAttribute constructer should throw ArgumentNullException when parameter exceptionType = null
        /// </summary>
        [TestFrameworkV1.TestMethod]
        public void ExpectedExceptionAttributeConstructerShouldThrowArgumentNullExceptionWhenExceptionTypeIsNull()
        {
            Action a = () => new TestFrameworkV2.ExpectedExceptionAttribute(null, "Dummy");

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentNullException));
        }

        /// <summary>
        /// ExpectedExceptionAttribute constructer should throw ArgumentNullException when parameter exceptionType = typeof(AnyClassNotDerivedFromExceptionClass)
        /// </summary>
        [TestFrameworkV1.TestMethod]
        public void ExpectedExceptionAttributeConstructerShouldThrowArgumentException()
        {
            Action a = () => new TestFrameworkV2.ExpectedExceptionAttribute(typeof(ExpectedExceptionAttributeTests), "Dummy");

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(ArgumentException));
        }

        /// <summary>
        /// ExpectedExceptionAttribute constructer should not throw exception when parameter exceptionType = typeof(AnyClassDerivedFromExceptionClass)
        /// </summary>
        [TestFrameworkV1.TestMethod]
        public void ExpectedExceptionAttributeConstructerShouldNotThrowAnyException()
        {
            TestFrameworkV2.ExpectedExceptionAttribute sut = new TestFrameworkV2.ExpectedExceptionAttribute(typeof(DummyTestClassDerivedFromException), "Dummy");
        }

        [TestFrameworkV1.TestMethod]
        public void GetExceptionMsgShouldReturnExceptionMessage()
        {
            Exception ex = new Exception("Dummy Exception");
            var actualMessage = TestFrameworkV2.UtfHelper.GetExceptionMsg(ex);
            var expectedMessage = "System.Exception: Dummy Exception";
            TestFrameworkV2.Assert.AreEqual(expectedMessage, actualMessage);
        }

        [TestFrameworkV1.TestMethod]
        public void GetExceptionMsgShouldReturnInnerExceptionMessageAsWellIfPresent()
        {
            Exception innerException = new DivideByZeroException();
            Exception ex = new Exception("Dummy Exception",innerException);
            var actualMessage = TestFrameworkV2.UtfHelper.GetExceptionMsg(ex);
            var expectedMessage = "System.Exception: Dummy Exception ---> System.DivideByZeroException: Attempted to divide by zero.";
            TestFrameworkV2.Assert.AreEqual(expectedMessage, actualMessage);
        }

        [TestFrameworkV1.TestMethod]
        public void GetExceptionMsgShouldReturnInnerExceptionMessageRecursivelyIfPresent()
        {
            Exception recursiveInnerException = new IndexOutOfRangeException("ThirdLevelException");
            Exception innerException = new DivideByZeroException("SecondLevel Exception", recursiveInnerException);
            Exception ex = new Exception("FirstLevelException", innerException);
            var actualMessage = TestFrameworkV2.UtfHelper.GetExceptionMsg(ex);
            var expectedMessage = "System.Exception: FirstLevelException ---> System.DivideByZeroException: SecondLevel Exception ---> System.IndexOutOfRangeException: ThirdLevelException";
            TestFrameworkV2.Assert.AreEqual(expectedMessage, actualMessage);
        }
    }

    /// <summary>
    /// Dummy class derived from Exception
    /// </summary>
    public class DummyTestClassDerivedFromException : Exception
    {
    }
}
