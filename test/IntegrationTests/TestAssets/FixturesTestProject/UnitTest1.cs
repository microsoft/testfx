// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FixturesTestProject1;

[TestClass]
public class UnitTest1
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        System.Diagnostics.Debug.Assert(false);
        bool? condition = GetCondition("AssemblyInitialize");
        Assert.IsNotNull(condition);
        Assert.IsTrue(condition.Value);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        bool? condition = GetCondition("AssemblyCleanup");
        Assert.IsNotNull(condition);
        Assert.IsTrue(condition.Value);
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        bool? condition = GetCondition("ClassInitialize");
        Assert.IsNotNull(condition);
        Assert.IsTrue(condition.Value);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        bool? condition = GetCondition("ClassCleanup");
        Assert.IsNotNull(condition);
        Assert.IsTrue(condition.Value);
    }

    [TestMethod]
    public void Test()
    {
        bool? condition = GetCondition("Test");
        Assert.IsNotNull(condition);
        Assert.IsTrue(condition.Value);
    }

    private static bool? GetCondition(string environmentVariable)
        => bool.TryParse(Environment.GetEnvironmentVariable(environmentVariable), out bool result)
        ? result
        : null;
}
