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
    /// Tests for class GenericParameterHelper
    /// </summary>
    [TestFrameworkV1.TestClass]
    public class GenericParameterHelperTests
    {
        private TestFrameworkV2.GenericParameterHelper sut = null;

        /// <summary>
        /// Test initialization function.
        /// </summary>
        [TestFrameworkV1.TestInitialize]
        public void TestInitialize()
        {
            this.sut = new TestFrameworkV2.GenericParameterHelper(10);
        }

        [TestFrameworkV1.TestMethod]
        public void EqualsShouldReturnTrueIfTwoObjectHasSameDataValue()
        {
            TestFrameworkV2.GenericParameterHelper objectToCompare = new TestFrameworkV2.GenericParameterHelper(10);

            TestFrameworkV1.Assert.IsTrue(this.sut.Equals(objectToCompare));
        }

        [TestFrameworkV1.TestMethod]
        public void EqualsShouldReturnFalseIfTwoObjectDoesNotHaveSameDataValue()
        {
            TestFrameworkV2.GenericParameterHelper objectToCompare = new TestFrameworkV2.GenericParameterHelper(5);

            TestFrameworkV1.Assert.IsFalse(this.sut.Equals(objectToCompare));
        }

        [TestFrameworkV1.TestMethod]
        public void CompareToShouldReturnZeroIfTwoObjectHasSameDataValue()
        {
            TestFrameworkV2.GenericParameterHelper objectToCompare = new TestFrameworkV2.GenericParameterHelper(10);

            TestFrameworkV1.Assert.AreEqual(this.sut.CompareTo(objectToCompare), 0);
        }

        [TestFrameworkV1.TestMethod]
        public void CompareToShouldThrowExceptionIfSpecifiedObjectIsNotOfTypeGenericParameterHelper()
        {
            int objectToCompare = 5;

            Action a = () => this.sut.CompareTo(objectToCompare);

            ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(NotSupportedException));
        }

        [TestFrameworkV1.TestMethod]
        public void GenericParameterHelperShouldImplementIEnumerator()
        {
            this.sut = new TestFrameworkV2.GenericParameterHelper(15);

            int expectedLenghtOfList = 5;  //(15%10)
            int result = 0;

            foreach (var x in this.sut)
            {
                result++;
            }

            TestFrameworkV1.Assert.AreEqual(result, expectedLenghtOfList);
        }
    }
}
