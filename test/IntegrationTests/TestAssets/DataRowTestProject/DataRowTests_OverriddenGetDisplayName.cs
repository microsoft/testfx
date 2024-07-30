// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowAttributeTestProject;

[TestClass]
public class DataRowTests_OverriddenGetDisplayName
{
    [TestCategory("OverriddenGetDisplayName")]
    [DummyDataRow]
    [TestMethod]
    public void TestMethod() => Assert.IsTrue(true);

    private class DummyDataRowAttribute : DataRowAttribute
    {
        public override string GetDisplayName(MethodInfo methodInfo, object[] data) => "Overridden DisplayName";
    }

    [TestMethod("SomeCustomDisplayName2")]
    [TestCategory("OverriddenTestMethodDisplayNameForParameterizedTest")]
    [DataRow("SomeData")]
    public void TestMethod2(string dataRowObject)
       => Assert.IsTrue(true);

    [TestMethod("SomeCustomDisplayName3")]
    [TestCategory("OverriddenTestMethodDisplayNameForParameterizedTest")]
    [DynamicData(nameof(Data))]
    public void TestMethod3(string dataRowObject)
       => Assert.IsTrue(true);

    public static IEnumerable<object[]> Data
    {
        get
        {
            yield return new object[] { "SomeData" };
        }
    }
}
