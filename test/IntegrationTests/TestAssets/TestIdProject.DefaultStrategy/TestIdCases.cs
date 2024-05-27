// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

#if LEGACY_TEST_ID
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.Legacy)]
#pragma warning restore CS0618 // Type or member is obsolete
#elif DISPLAY_NAME_TEST_ID
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.DisplayName)]
#pragma warning restore CS0618 // Type or member is obsolete
#elif FULLY_QUALIFIED_ID
[assembly: TestIdGenerationStrategy(TestIdGenerationStrategy.FullyQualified)]
#endif

namespace TestIdProject;

[TestClass]
public class TestIdCases
{
    [TestMethod] // See https://github.com/microsoft/testfx/issues/1016
    [DataRow(0, new int[] { })]
    [DataRow(0, new int[] { 0 })]
    [DataRow(0, new int[] { 0, 0, 0 })]
    public void DataRowArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    [TestMethod] // See https://github.com/microsoft/testfx/issues/1028
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")] // space
    [DataRow("  ")] // tab
    public void DataRowStringTests(string s)
    {
    }

    [DataTestMethod]
    [DynamicData(nameof(ArraysData))]
    public void DynamicDataArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    public static IEnumerable<object[]> ArraysData
    {
        get
        {
            yield return [0, Array.Empty<int>()];
            yield return [0, new int[] { 0 }];
            yield return [0, new int[] { 0, 0, 0 }];
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(TuplesData))]
    public void DynamicDataTuplesTests((int I, string S, bool B) tuple)
    {
    }

    public static IEnumerable<object[]> TuplesData
    {
        get
        {
            yield return [(1, "text", true)];
            yield return [(1, "text", false)];
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(GenericCollectionsData))]
    public void DynamicDataGenericCollectionsTests(List<int> integers, List<string> strings, List<bool> bools)
    {
    }

    public static IEnumerable<object[]> GenericCollectionsData
    {
        get
        {
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b" }, new List<bool> { true, false, true }];
            yield return [new List<int> { 1, 2, 3 }, new List<string> { "a", "b", "c" }, new List<bool> { true, false }];
        }
    }

    [DataTestMethod]
    [ArraysDataSource]
    public void TestDataSourceArraysTests(int expectedSum, int[] array) => Assert.AreEqual(expectedSum, array.Sum());

    public class ArraysDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => ArraysData;

        public string GetDisplayName(MethodInfo methodInfo, object[] data) => "Custom name";
    }

    [DataTestMethod]
    [TuplesDataSource]
    public void TestDataSourceTuplesTests((int I, string S, bool B) tuple)
    {
    }

    public class TuplesDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => TuplesData;

        public string GetDisplayName(MethodInfo methodInfo, object[] data) => "Custom name";
    }

    [DataTestMethod]
    [GenericCollectionsDataSource]
    public void TestDataSourceGenericCollectionsTests(List<int> integers, List<string> strings, List<bool> bools)
    {
    }

    public class GenericCollectionsDataSourceAttribute : Attribute, ITestDataSource
    {
        public IEnumerable<object[]> GetData(MethodInfo methodInfo) => GenericCollectionsData;

        public string GetDisplayName(MethodInfo methodInfo, object[] data) => "Custom name";
    }
}
