// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DynamicDataTestProject;

[TestClass]
public sealed class DisableExpansionTests
{
    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(PropertySource))]
    public void TestPropertySourceOnCurrentType(int a, string s)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(MethodSource))]
    public void TestMethodSourceOnCurrentType(int a, string s)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(PropertySource), typeof(DataSourceHelper))]
    public void TestPropertySourceOnDifferentType(int a, string s)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)]
    [DynamicData(nameof(MethodSource), typeof(DataSourceHelper))]
    public void TestMethodSourceOnDifferentType(int a, string s)
    {
    }

    private static IEnumerable<object[]> PropertySource => MethodSource();

    private static IEnumerable<object[]> MethodSource()
    {
        yield return [1, "a"];
        yield return [2, "b"];
    }
}

public class DataSourceHelper
{
    public static IEnumerable<object[]> PropertySource => MethodSource();

    public static IEnumerable<object[]> MethodSource()
    {
        yield return [3, "c"];
        yield return [4, "d"];
    }
}
