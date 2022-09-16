// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace UnitTestFramework.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

/// <summary>
/// Tests for class GenericParameterHelper
/// </summary>
public class GenericParameterHelperTests : TestContainer
{
    private GenericParameterHelper _sut = null;

    /// <summary>
    /// Test initialization function.
    /// </summary>
    public GenericParameterHelperTests()
    {
        _sut = new GenericParameterHelper(10);
    }

    public void EqualsShouldReturnFalseIfEachObjectHasDefaultDataValue()
    {
        GenericParameterHelper firstObject = new();
        GenericParameterHelper secondObject = new();

        Verify(!firstObject.Equals(secondObject));
    }

    public void EqualsShouldReturnTrueIfTwoObjectHasSameDataValue()
    {
        GenericParameterHelper objectToCompare = new(10);

        Verify(_sut.Equals(objectToCompare));
    }

    public void EqualsShouldReturnFalseIfTwoObjectDoesNotHaveSameDataValue()
    {
        GenericParameterHelper objectToCompare = new(5);

        Verify(!_sut.Equals(objectToCompare));
    }

    public void CompareToShouldReturnZeroIfTwoObjectHasSameDataValue()
    {
        GenericParameterHelper objectToCompare = new(10);

        Verify(0 == _sut.CompareTo(objectToCompare));
    }

    public void CompareToShouldThrowExceptionIfSpecifiedObjectIsNotOfTypeGenericParameterHelper()
    {
        int objectToCompare = 5;

        void a() => _sut.CompareTo(objectToCompare);

        var ex = VerifyThrows(a);
        Verify(ex.GetType() == typeof(NotSupportedException));
    }

    public void GenericParameterHelperShouldImplementIEnumerator()
    {
        _sut = new GenericParameterHelper(15);

        int expectedLenghtOfList = 5;  // (15%10)
        int result = 0;

        foreach (var x in _sut)
        {
            result++;
        }

        Verify(result == expectedLenghtOfList);
    }
}
