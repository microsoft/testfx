// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using MSTestAdapter.TestUtilities;

using TestFrameworkV1 = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting;
using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for class GenericParameterHelper
/// </summary>
[TestFrameworkV1.TestClass]
public class GenericParameterHelperTests
{
    private TestFrameworkV2.GenericParameterHelper _sut = null;

    /// <summary>
    /// Test initialization function.
    /// </summary>
    [TestFrameworkV1.TestInitialize]
    public void TestInitialize()
    {
        _sut = new TestFrameworkV2.GenericParameterHelper(10);
    }

    [TestFrameworkV1.TestMethod]
    public void EqualsShouldReturnFalseIfEachObjectHasDefaultDataValue()
    {
        TestFrameworkV2.GenericParameterHelper firstObject = new();
        TestFrameworkV2.GenericParameterHelper secondObject = new();

        TestFrameworkV1.Assert.IsFalse(firstObject.Equals(secondObject));
    }

    [TestFrameworkV1.TestMethod]
    public void EqualsShouldReturnTrueIfTwoObjectHasSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(10);

        TestFrameworkV1.Assert.IsTrue(_sut.Equals(objectToCompare));
    }

    [TestFrameworkV1.TestMethod]
    public void EqualsShouldReturnFalseIfTwoObjectDoesNotHaveSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(5);

        TestFrameworkV1.Assert.IsFalse(_sut.Equals(objectToCompare));
    }

    [TestFrameworkV1.TestMethod]
    public void CompareToShouldReturnZeroIfTwoObjectHasSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(10);

        TestFrameworkV1.Assert.AreEqual(0, _sut.CompareTo(objectToCompare));
    }

    [TestFrameworkV1.TestMethod]
    public void CompareToShouldThrowExceptionIfSpecifiedObjectIsNotOfTypeGenericParameterHelper()
    {
        int objectToCompare = 5;

        void a() => _sut.CompareTo(objectToCompare);

        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(NotSupportedException));
    }

    [TestFrameworkV1.TestMethod]
    public void GenericParameterHelperShouldImplementIEnumerator()
    {
        _sut = new TestFrameworkV2.GenericParameterHelper(15);

        int expectedLenghtOfList = 5;  // (15%10)
        int result = 0;

        foreach (var x in _sut)
        {
            result++;
        }

        TestFrameworkV1.Assert.AreEqual(result, expectedLenghtOfList);
    }
}
