// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
public class DataRowTests_Index
{
    #region // https://github.com/microsoft/testfx/issues/2390

    [TestMethod]
    [DataRow((byte)0, new object[] { (byte)0 }, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    [DataRow((short)0, new object[] { (short)0 }, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    [DataRow(0L, new object[] { 0L }, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void NestedInputTypes(object org, object nested)
        => Assert.IsTrue(
            org.GetType().Name.Equals(((object[])nested)[0].GetType().Name, StringComparison.Ordinal),
            string.Concat("Expected ", org.GetType().Name, " but got ", ((object[])nested)[0].GetType().Name));

    [TestMethod]
    [DataRow(0, new object[] { (byte)0 }, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.UnfoldUsingDataIndex)]
    public void NestedInputTypesInvalid(object org, object nested)
        => Assert.IsFalse(
            org.GetType().Name.Equals(((object[])nested)[0].GetType().Name, StringComparison.Ordinal),
            string.Concat("Expected ", org.GetType().Name, " but got ", ((object[])nested)[0].GetType().Name));

    #endregion
}
