// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DynamicDataTestProject;

[TestClass]
public sealed class DisableExpansionTests
{
    [TestMethod]
    [DynamicData(nameof(PropertySource), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestPropertySourceOnCurrentType(int a, string s)
    {
    }

    [TestMethod]
    [DynamicData(nameof(MethodSource), DynamicDataSourceType.Method, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestMethodSourceOnCurrentType(int a, string s)
    {
    }

    [TestMethod]
    [DynamicData(nameof(PropertySource), typeof(DataSourceHelper), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestPropertySourceOnDifferentType(int a, string s)
    {
    }

    [TestMethod]
    [DynamicData(nameof(MethodSource), typeof(DataSourceHelper), DynamicDataSourceType.Method, UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestMethodSourceOnDifferentType(int a, string s)
    {
    }

    [TestMethod]
    [DynamicData(nameof(PropertySource), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(PropertySource), typeof(DataSourceHelper))]
    public void TestPropertyWithTwoSourcesAndFirstDisablesExpansion(int a, string s)
    {
    }

    [TestMethod]
    [DynamicData(nameof(PropertySource))]
    [DynamicData(nameof(PropertySource), typeof(DataSourceHelper), UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    public void TestPropertyWithTwoSourcesAndSecondDisablesExpansion(int a, string s)
    {
    }

    private static IEnumerable<object[]> PropertySource => MethodSource();

    private static IEnumerable<object[]> MethodSource()
    {
        yield return new object[] { 1, "a" };
        yield return new object[] { 2, "b" };
    }
}

public class DataSourceHelper
{
    public static IEnumerable<object[]> PropertySource => MethodSource();

    public static IEnumerable<object[]> MethodSource()
    {
        yield return new object[] { 3, "c" };
        yield return new object[] { 4, "d" };
    }
}
