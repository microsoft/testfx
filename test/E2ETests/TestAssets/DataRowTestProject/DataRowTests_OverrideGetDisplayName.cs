// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowAttributeTestProject;

[TestClass]
public class DataRowTests_OverrideGetDisplayName : DataRowAttribute
{
    [TestCategory("OverridedGetDiplayName")]
    [DummyDataRowAttribute]
    [TestMethod]
    public void TestMethod()
    {
        Assert.IsTrue(true);
    }

    private class DummyDataRowAttribute : DataRowAttribute
    {
        public DummyDataRowAttribute()
            : base()
        {
        }

        public override string GetDisplayName(MethodInfo methodInfo, object[] data)
        {
            return "Overrided DisplayName";
        }
    }
}
