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
    /// Tests for class ExpectedExceptionBaseAttribute
    /// </summary>
    [TestFrameworkV1.TestClass]
    public class ExpectedExceptionBaseAttributeTests
    {
        private TestableExpectedExceptionBaseAttributeClass sut = null;

        /// <summary>
        /// Test initialization function.
        /// </summary>
        [TestFrameworkV1.TestInitialize]
        public void TestInitialize()
        {
            this.sut = new TestableExpectedExceptionBaseAttributeClass();
        }

        /// <summary>
        /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertFailedException as parameter in it.
        /// </summary>
        [TestFrameworkV1.TestMethod]
        public void RethrowIfAssertExceptionThrowsExceptionOnAssertFailure()
        {
            Action a = () => this.sut.RethrowIfAssertException(new TestFrameworkV2.AssertFailedException());

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(TestFrameworkV2.AssertFailedException));
        }

        /// <summary>
        /// RethrowIfAssertException function will throw AssertFailedException if we pass AssertInconclusiveException as parameter in it.
        /// </summary>
        [TestFrameworkV1.TestMethod]
        public void RethrowIfAssertExceptionThrowsExceptionOnAssertInconclusive()
        {
            Action a = () => this.sut.RethrowIfAssertException(new TestFrameworkV2.AssertInconclusiveException());

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(TestFrameworkV2.AssertInconclusiveException));
        }

        [TestFrameworkV1.TestMethod]
        public void VerifyCorrectMessageIsGettingSetInVariablenoExceptionMessage()
        {
            string expected = "DummyString";
            this.sut = new TestableExpectedExceptionBaseAttributeClass(expected);

            string result = this.sut.GetNoExceptionMessage();

            TestFrameworkV1.Assert.AreEqual(expected, result);
        }

        [TestFrameworkV1.TestMethod]
        public void VerifyEmptytMessageIsGettingSetInVariablenoExceptionMessage()
        {
            this.sut = new TestableExpectedExceptionBaseAttributeClass(null);

            string result = this.sut.GetNoExceptionMessage();

            TestFrameworkV1.Assert.IsTrue(String.IsNullOrEmpty(result));
        }
    }

    /// <summary>
    /// Dummy class derived from Exception
    /// </summary>
    public class TestableExpectedExceptionBaseAttributeClass : TestFrameworkV2.ExpectedExceptionBaseAttribute
    {
        public TestableExpectedExceptionBaseAttributeClass() : base()
        {
        }

        public TestableExpectedExceptionBaseAttributeClass(string noExceptionMessage) : base(noExceptionMessage)
        {
        }

        /// <summary>
        /// Rethrow the exception if it is an AssertFailedException or an AssertInconclusiveException
        /// </summary>
        /// <param name="exception">The exception to rethrow if it is an assertion exception</param>
        public new void RethrowIfAssertException(Exception exception)
        {
            base.RethrowIfAssertException(exception);
        }
    }
}
