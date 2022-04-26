// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;

    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

    [TestClass]
    public class ThreadSafeStringWriterTests
    {
        private bool task2flag;

        [TestMethod]
        public void ThreadSafeStringWriterWriteLineHasContentFromMultipleThreads()
        {
            using (ExecutionContext.SuppressFlow())
            {
                using (var stringWriter = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "tst"))
                {
                    Action<string> action = (string x) =>
                        {
                            var count = 10;
                            for (var i = 0; i < count; i++)
                            {
                                // Choose WriteLine since it calls the entire sequence:
                                // Write(string) -> Write(char[]) -> Write(char)
                                stringWriter.WriteLine(x);
                            }
                        };

                    var task1 = Task.Run(() =>
                    {
                        var timeout = Stopwatch.StartNew();
                        action("content1");
                        action("content1");
                        action("content1");
                        action("content1");
                        while (this.task2flag != true && timeout.Elapsed < TimeSpan.FromSeconds(5))
                        {
                        }
                        action("content1");
                        action("content1");
                        action("content1");
                        action("content1");
                    });
                    var task2 = Task.Run(() =>
                    {
                        action("content2");
                        action("content2");
                        action("content2");
                        action("content2");
                        this.task2flag = true;
                        action("content2");
                        action("content2");
                        action("content2");
                        action("content2");
                    });

                    task2.GetAwaiter().GetResult();
                    task1.GetAwaiter().GetResult();

                    var content = stringWriter.ToString();
                    content.Should().NotBeNullOrWhiteSpace();

                    // Validate that only whole lines are written, not a mix of random chars
                    var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    lines.Should().HaveCountGreaterThan(0);
                    foreach (var line in lines)
                    {
                        Assert.IsTrue(line.Equals("content1") || line.Equals("content2"));
                    }
                }
            }
        }
    }
}
