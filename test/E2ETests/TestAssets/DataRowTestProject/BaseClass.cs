// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

namespace DataRowTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseClass
    {
        [TestMethod]
        [DataRow("BaseString1")]
        [DataRow("BaseString2")]
        [DataRow("BaseString3")]
        public virtual void DataRowTestMethod(string a)
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        [DataRow(42)]
        [DataRow(42, "BaseOptionalString1")]
        [DataRow(42, "BaseOptionalString2", "BaseOptionalString3")]
        [DataRow(42, "BaseOptionalString4", "BaseOptionalString5")]
        public virtual void DataRowTestMethodWithSomeOptionalParameters(int i, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        [DataRow()]
        [DataRow(42)]
        [DataRow(42, "BaseOptionalString6")]
        [DataRow(42, "BaseOptionalString7", "BaseOptionalString8")]
        [DataRow(42, "BaseOptionalString9", "BaseOptionalString10")]
        public virtual void DataRowTestMethodWithAllOptionalParameters(int i = 0, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }
    }
}
