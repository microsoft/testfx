// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

namespace DataRowTestProject
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BaseClass
    {
        [TestCategory("DataRowSimple")]
        [TestMethod]
        [DataRow("BaseString1")]
        [DataRow("BaseString2")]
        [DataRow("BaseString3")]
        public virtual void DataRowTestMethod(string a)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowSomeOptional")]
        [TestMethod]
        [DataRow(42)]
        [DataRow(42, "BaseOptionalString1")]
        [DataRow(42, "BaseOptionalString2", "BaseOptionalString3")]
        [DataRow(42, "BaseOptionalString4", "BaseOptionalString5")]
        public virtual void DataRowTestMethodWithSomeOptionalParameters(int i, string s1 = null, string s2 = null)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowAllOptional")]
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

        [TestCategory("DataRowParamsArgument")]
        [TestMethod]
        [DataRow(1)]
        [DataRow(1, "BaseSingleParamsArg")]
        [DataRow(1, "BaseParamsArg1","BaseParamsArg2")]
        [DataRow(1, "BaseParamsArg1", "BaseParamsArg2", "BaseParamsArg3")]
        public virtual void DataRowTestMethodWithParamsParameters(int i, params string[] args)
        {
            Assert.IsTrue(true);
        }

        [TestCategory("DataRowOptionalInvalidArguments")]
        [TestMethod]
        [ExpectedException(typeof(System.Reflection.TargetParameterCountException))]
        [DataRow()]
        [DataRow(1)]
        [DataRow(1, "BaseRequiredArgument", "BaseOptionalArgument", "BaseExtraArgument")]
        public virtual void DataRowTestMethodFailsWithInvalidArguments(int i1, string required, string s1 = null)
        {
            Assert.Fail();
        }
    }
}
