// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploymentTestProject.PreserveNewest;

[TestClass]
public class FileDeploymentTests
{
    [TestMethod]
    [DeploymentItem(@"TestFiles1/some_file1")]
    public void FileWithForwardSlash()
    {
        FileStream fs = File.Open(@"some_file1", FileMode.Open);
        fs.Close();
    }

    [TestMethod]
    [DeploymentItem(@"TestFiles2\some_file2")]
    public void FileWithBackSlash()
    {
        FileStream fs = File.Open(@"some_file2", FileMode.Open);
        fs.Close();
    }
}
