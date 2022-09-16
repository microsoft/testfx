// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests;

using System;
using global::TestFramework.ForTestingMSTest;

/// <summary>
/// Tests for class GenericParameterHelper
/// </summary>
public class GenericParameterHelperTests : TestContainer
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

    public void EqualsShouldReturnFalseIfEachObjectHasDefaultDataValue()
    {
        TestFrameworkV2.GenericParameterHelper firstObject = new();
        TestFrameworkV2.GenericParameterHelper secondObject = new();

        TestFrameworkV1.Assert.IsFalse(firstObject.Equals(secondObject));
    }

    public void EqualsShouldReturnTrueIfTwoObjectHasSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(10);

        TestFrameworkV1.Assert.IsTrue(_sut.Equals(objectToCompare));
    }

    public void EqualsShouldReturnFalseIfTwoObjectDoesNotHaveSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(5);

        TestFrameworkV1.Assert.IsFalse(_sut.Equals(objectToCompare));
    }

    public void CompareToShouldReturnZeroIfTwoObjectHasSameDataValue()
    {
        TestFrameworkV2.GenericParameterHelper objectToCompare = new(10);

        TestFrameworkV1.Assert.AreEqual(0, _sut.CompareTo(objectToCompare));
    }

    public void CompareToShouldThrowExceptionIfSpecifiedObjectIsNotOfTypeGenericParameterHelper()
    {
        int objectToCompare = 5;

        void a() => _sut.CompareTo(objectToCompare);

        ActionUtility.ActionShouldThrowExceptionOfType(a, typeof(NotSupportedException));
    }

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
