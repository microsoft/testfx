// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

#if LEGACY_TEST_ID
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.Legacy)]
#elif DISPLAY_NAME_TEST_ID
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.DisplayName)]
#elif FULLY_QUALIFIED_TEST_ID
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.FullyQualifiedTest)]
#endif

namespace TestIdProject.LegacyStrategy;

[TestClass]
public class TestIdCases
{
    [TestMethod] // See https://github.com/microsoft/testfx/issues/1016
    [DataRow(0, new int[] { })]
    [DataRow(0, new int[] { 0 })]
    [DataRow(0, new int[] { 0, 0, 0 })]
    public void DataRowArraysTests(int expectedSum, int[] array)
    {
        Assert.AreEqual(expectedSum, array.Sum());
    }

    [TestMethod] // See https://github.com/microsoft/testfx/issues/1028
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")] // space
    [DataRow("  ")] // tab
    public void DataRowStringTests(string parameter)
    {
    }    
}
