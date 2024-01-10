// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace UnitTestFramework.Tests;

/// <summary>
/// Tests for class GenericParameterHelper.
/// </summary>
public class GenericParameterHelperTests : TestContainer
{
    private GenericParameterHelper _sut;

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

        Verify(_sut.CompareTo(objectToCompare) == 0);
    }

    public void CompareToShouldThrowExceptionIfSpecifiedObjectIsNotOfTypeGenericParameterHelper()
    {
        int objectToCompare = 5;

        void A() => _sut.CompareTo(objectToCompare);

        var ex = VerifyThrows(A);
        Verify(ex is NotSupportedException);
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
