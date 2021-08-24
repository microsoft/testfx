﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataSourceTestProject.ITestDataSourceTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using System;

    [TestClass]
    public class DataRowTests_NonSerializablePaths
    {
        [TestMethod]
        [DataRow(typeof(string))]
        [DataRow(typeof(int))]
        [DataRow(typeof(DataRowTests_Enums))]
        public void DataRowNonSerializable(Type type)
        {
            Assert.IsTrue(true);
        }
    }
}
