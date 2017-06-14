// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using System.Linq;

    using CollectionAssert = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using DataRowAttribute = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.DataRowAttribute;
    using TestClass = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1.Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class DataRowAttributeTests
    {
        [TestMethod]
        public void ConstructorShouldSetDataPassed()
        {
            var dataRow = new DataRowAttribute("mercury");

            CollectionAssert.AreEqual(new object[] { "mercury" }, dataRow.Data);
        }

        [TestMethod]
        public void ConstructorShouldSetMultipleDataValuesPassed()
        {
            var dataRow = new DataRowAttribute("mercury", "venus", "earth");

            CollectionAssert.AreEqual(new object[] { "mercury", "venus", "earth" }, dataRow.Data);
        }

        [TestMethod]
        public void ConstructorShouldSetANullDataValuePassedInParams()
        {
            var dataRow = new DataRowAttribute("neptune", null);

            CollectionAssert.AreEqual(new object[] { "neptune", null }, dataRow.Data);
        }

        [TestMethod]
        public void ConstructorShouldSetANullDataValuePassedInAsADataArg()
        {
            var dataRow = new DataRowAttribute(null, "logos");

            CollectionAssert.AreEqual(new object[] { null, "logos" }, dataRow.Data);
        }

        [TestMethod]
        public void GetDataShouldReturnDataPassed()
        {
            var dataRow = new DataRowAttribute("mercury");

            CollectionAssert.AreEqual(new object[] { "mercury" }, dataRow.GetData(null).FirstOrDefault());
        }
    }
}
