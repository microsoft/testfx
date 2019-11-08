// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DataRowTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DerivedClass : BaseClass
    {
        [TestCategory("DataRowSimple")]
        [TestMethod]
        [DataRow("DerivedString1")]
        [DataRow("DerivedString2")]
        public override void DataRowTestMethod(string a)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowSomeOptional")]
        [TestMethod]
        [DataRow(123)]
        [DataRow(123, "DerivedOptionalString1")]
        [DataRow(123, "DerivedOptionalString2", "DerivedOptionalString3")]
        public void DataRowTestMethodWithSomeOptionalParameters(int i, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowAllOptional")]
        [TestMethod]
        [DataRow()]
        [DataRow(123)]
        [DataRow(123, "DerivedOptionalString4")]
        [DataRow(123, "DerivedOptionalString5", "DerivedOptionalString6")]
        public void DataRowTestMethodWithAllOptionalParameters(int i = 0, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }
        
        [TestCategory("DataRowParamsArgument")]
        [TestMethod]
        [DataRow(2)]
        [DataRow(2, "DerivedSingleParamsArg")]
        [DataRow(2, "DerivedParamsArg1", "DerivedParamsArg2")]
        [DataRow(2, "DerivedParamsArg1", "DerivedParamsArg2","DerivedParamsArg3")]
        public void DataRowTestMethodWithParamsParameters(int i, params string[] args)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowOptionalInvalidArguments")]
        [TestMethod]
        [ExpectedException(typeof(System.Reflection.TargetParameterCountException))]
        [DataRow()]
        [DataRow(2)]
        [DataRow(2, "DerivedRequiredArgument", "DerivedOptionalArgument", "DerivedExtraArgument")]
        public void DataRowTestMethodFailsWithInvalidArguments(int i1, string requiredString, string s1 = null)
        {
            Assert.Fail();
        }
    }
}
