// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

public class DataRowAttributeTests : TestContainer
{
    private DummyTestClass _dummyTestClass;
    private MethodInfo _testMethodInfo;

    public void DefaultConstructorSetsEmptyArrayPassed()
    {
        var dataRow = new DataRowAttribute();

        Verify(System.Array.Empty<object>().SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        Verify(new object[] { "mercury" }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetMultipleDataValuesPassed()
    {
        var dataRow = new DataRowAttribute("mercury", "venus", "earth");

        Verify(new object[] { "mercury", "venus", "earth" }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetANullDataValuePassedInParams()
    {
        var dataRow = new DataRowAttribute("neptune", null);

        Verify(new object[] { "neptune", null }.SequenceEqual(dataRow.Data));
    }

    public void ConstructorShouldSetANullDataValuePassedInAsADataArg()
    {
        var dataRow = new DataRowAttribute(null, "logos");

        Verify(new object[] { null, "logos" }.SequenceEqual(dataRow.Data));
    }

    public void GetDataShouldReturnDataPassed()
    {
        var dataRow = new DataRowAttribute("mercury");

        Verify(new object[] { "mercury" }.SequenceEqual(dataRow.GetData(null).FirstOrDefault()));
    }

    public void GetDisplayNameShouldReturnAppropriateName()
    {
        var dataRowAttribute = new DataRowAttribute(null);

        _dummyTestClass = new DummyTestClass();
        _testMethodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        var data = new string[] { "First", "Second", null };
        var data1 = new string[] { null, "First", "Second" };
        var data2 = new string[] { "First", null, "Second" };

        var displayName = dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        Verify("DataRowTestMethod (First,Second,)" == displayName);

        displayName = dataRowAttribute.GetDisplayName(_testMethodInfo, data1);
        Verify("DataRowTestMethod (,First,Second)" == displayName);

        displayName = dataRowAttribute.GetDisplayName(_testMethodInfo, data2);
        Verify("DataRowTestMethod (First,,Second)" == displayName);
    }

    public void GetDisplayNameShouldReturnSpecifiedDisplayName()
    {
        var dataRowAttribute = new DataRowAttribute(null)
        {
            DisplayName = "DataRowTestWithDisplayName"
        };

        _dummyTestClass = new DummyTestClass();
        _testMethodInfo = _dummyTestClass.GetType().GetTypeInfo().GetDeclaredMethod("DataRowTestMethod");

        var data = new string[] { "First", "Second", null };

        var displayName = dataRowAttribute.GetDisplayName(_testMethodInfo, data);
        Verify("DataRowTestWithDisplayName" == displayName);
    }
}
