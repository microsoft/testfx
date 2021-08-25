// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
#if NETCOREAPP
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UnitTestOutcome = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
#endif

    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

    [TestClass]
    public class ThreadSafeStringWriterTests
    {
        private bool task2flag;

        [TestMethod]
        public void ThreadSafeStringWriterWritesLinesFromDifferentsTasksSeparately()
        {
            // String writer needs to be task aware to write output from different tasks
            // into different output. The tasks below wait for each other to ensure
            // we are mixing output from different tasks at the same time.
            using (var stringWriter = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out"))
            {
                var task1 = Task.Run(() =>
                {
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    while (this.task2flag != true)
                    {
                    }
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    stringWriter.WriteLine("content1");
                    return stringWriter.ToString();
                });
                var task2 = Task.Run(() =>
                {
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    this.task2flag = true;
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    stringWriter.WriteLine("content2");
                    return stringWriter.ToString();
                });

                var task1Output = task1.GetAwaiter().GetResult();
                var task2Output = task2.GetAwaiter().GetResult();

                // there was no output in the current task, the output should be empty
                var content = stringWriter.ToString();
                content.Should().BeNullOrWhiteSpace();

                // task1 and task2 should output into their respective buckets
                task1Output.Should().NotBeNullOrWhiteSpace();
                task2Output.Should().NotBeNullOrWhiteSpace();

                task1Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content1");
                task2Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content2");
            }
        }

        [TestMethod]
        public void ThreadSafeStringWriterWritesLinesIntoDifferentWritesSeparately()
        {
            // The string writer must not mix output captured by different instances to avoid mixing different kinds of output
            // e.g. mixing Standard output with Debug output, because each one of them is given their own instance of StringWriter
            using (var stringWriter1 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out"))
            {
                using (var stringWriter2 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out"))
                {
                    stringWriter1.WriteLine("content1");
                    stringWriter2.WriteLine("content2");
                    stringWriter1.WriteLine("content1");
                    stringWriter2.WriteLine("content2");

                    var stringWriter1Output = stringWriter1.ToString();
                    var stringWriter2Output = stringWriter2.ToString();

                    // task1 and task2 should output into their respective buckets
                    stringWriter1Output.Should().NotBeNullOrWhiteSpace();
                    stringWriter2Output.Should().NotBeNullOrWhiteSpace();

                    stringWriter1Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content1");
                    stringWriter2Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content2");
                }
            }
        }
    }
}
