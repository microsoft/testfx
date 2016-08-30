// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ThreadSafeStringWriterTests.cs" company="">
//   
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    using System;
    using System.Globalization;
    using System.Threading.Tasks;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

    [TestClass]
    public class ThreadSafeStringWriterTests
    {
        [TestMethod]
        public void ThreadSafeStringWriterWriteLineHasContentFromMultipleThreads()
        {
            using (var stringWriter = new ThreadSafeStringWriter(CultureInfo.InvariantCulture))
            {
                Action<string> action = (string x) =>
                    {
                        for (var i = 0; i < 100000; i++)
                        {
                            // Choose WriteLine since it calls the entire sequence:
                            // Write(string) -> Write(char[]) -> Write(char)
                            stringWriter.WriteLine(x);
                        }
                    };

                var task1 = Task.Run(() => action("content1"));
                var task2 = Task.Run(() => action("content2"));

                task1.Wait();
                task2.Wait();

                // Validate that only whole lines are written, not a mix of random chars
                foreach (var line in stringWriter.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.IsTrue(line.Equals("content1") || line.Equals("content2"));
                }
            }
        }
    }
}
