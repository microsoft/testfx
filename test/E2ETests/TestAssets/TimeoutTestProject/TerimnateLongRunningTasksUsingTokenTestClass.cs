using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TimeoutTestProject
{
    [TestClass]
    public class TerimnateLongRunningTasksUsingTokenTestClass
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [Timeout(5000)]
        public void TerimnateLongRunningTasksUsingToken()
        {
            var longTask = new Thread(ExecuteLong);
            longTask.Start();
            longTask.Join();
        }

        private void ExecuteLong()
        {
            try
            {
                File.Delete("TimeoutTestOutput.txt");
                Task.Delay(100000).Wait(TestContext.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                File.WriteAllText("TimeoutTestOutput.txt", "Written from long running thread post termination");
            }
        }
    }
}
