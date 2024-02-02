// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace CustomReportExtension;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void PassedTestMethod()
    {
    }

    [TestMethod]
    public void FailedTestMethod()
    {
        Assert.Fail();
    }
}
