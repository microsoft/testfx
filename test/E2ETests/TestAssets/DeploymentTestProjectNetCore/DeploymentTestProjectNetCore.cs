using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DeploymentTestProjectNetCore
{
    [DeploymentItem(@"..\..\..\test\E2ETests\TestAssets\DeploymentTestProjectNetCore\DeploymentFile.xml")]
    [TestClass]
    public class DeploymentTestProjectNetCore
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

        [DeploymentItem(@"..\..\..\test\E2ETests\TestAssets\DeploymentTestProjectNetCore\TestCaseDeploymentFile.xml")]
        [TestMethod]
        public void PassIfDeclaredFilesPresent()
        {
            Assert.IsTrue(File.Exists("DeploymentFile.xml"));
            Assert.IsTrue(File.Exists("TestCaseDeploymentFile.xml"));
        }
    }
}
