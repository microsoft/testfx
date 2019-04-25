using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TimeoutTestProject
{
    [TestClass]
    public class SelfTerminatingTestClass
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Timeout(60000)]
        public void SelfTerminatingTestMethod()
        {
            TestContext.CancellationTokenSource.Cancel();
        }
    }
}
