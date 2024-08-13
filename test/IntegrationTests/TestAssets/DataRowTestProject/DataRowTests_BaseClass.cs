// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DataRowTestProject;

[TestClass]
public class DataRowTests_BaseClass
{
    [TestCategory("DataRowSimple")]
    [TestMethod]
    [DataRow("BaseString1")]
    [DataRow("BaseString2")]
    [DataRow("BaseString3")]
    public virtual void DataRowTestMethod(string a) => Assert.IsTrue(true);
}
