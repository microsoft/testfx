﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploymentTestProject.PreserveNewest;

[TestClass]
public class DirectoryDeploymentTests
{
    [TestMethod]
    [DeploymentItem(@"TestFiles1/")]
    public void DirectoryWithForwardSlash()
    {
        var fs = File.Open(@"some_file1", FileMode.Open);
        fs.Close();
    }

    [TestMethod]
    [DeploymentItem(@"TestFiles2\")]
    public void DirectoryWithBackSlash()
    {
        var fs = File.Open(@"some_file2", FileMode.Open);
        fs.Close();
    }
}
