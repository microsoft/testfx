// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace UnitTest;

[TestClass]
public class TestClass
{
    [UITestMethod]
    public void TestMethod1()
    {
        var grid = new Grid();
        Assert.IsNotNull(grid);
    }
}
