// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataRowTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DerivedClass : BaseClass
    {
        [TestMethod]
        [DataRow("DerivedString1")]
        [DataRow("DerivedString2")]
        public override void DataRowTestMethod(string a)
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        [DataRow(123)]
        [DataRow(123, "DerivedOptionalString1")]
        [DataRow(123, "DerivedOptionalString2", "DerivedOptionalString3")]
        public override void DataRowTestMethodWithSomeOptionalParameters(int i, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        [DataRow(123)]
        [DataRow(123, "DerivedOptionalString4")]
        [DataRow(123, "DerivedOptionalString5", "DerivedOptionalString6")]
        public override void DataRowTestMethodWithAllOptionalParameters(int i = 0, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }
    }
}
