using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploymentTestProject;

[DeploymentItem(@"..\..\..\..\test\IntegrationTests\TestAssets\DeploymentTestProject\DeploymentFile.xml")]
[TestClass]
public class DeploymentTestProject
{
    [TestMethod]
    public void PassIfFilePresent()
    {
        Assert.IsTrue(File.Exists("EmptyDataFile.xml"));
    }

    [TestMethod]
    public void FailIfFilePresent()
    {
        Assert.IsFalse(File.Exists("EmptyDataFile.xml"));
    }

    [DeploymentItem(@"..\..\..\..\test\IntegrationTests\TestAssets\DeploymentTestProject\TestCaseDeploymentFile.xml")]
    [TestMethod]
    public void PassIfDeclaredFilesPresent()
    {
        Assert.IsTrue(File.Exists("DeploymentFile.xml"));
        Assert.IsTrue(File.Exists("TestCaseDeploymentFile.xml"));
    }
}
