// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

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
using System.Diagnostics;
using System.Globalization;
using System.Threading;
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
        // Suppress the flow of parent context here becuase this test method will run in
        // a task already and we don't want the existing async context to interfere with this.
        using (ExecutionContext.SuppressFlow())
        {
            // String writer needs to be task aware to write output from different tasks
            // into different output. The tasks below wait for each other to ensure
            // we are mixing output from different tasks at the same time.
            using var stringWriter = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "output");
            var task1 = Task.Run(() =>
            {
                var timeout = Stopwatch.StartNew();
                stringWriter.WriteLine("content1");
                stringWriter.WriteLine("content1");
                stringWriter.WriteLine("content1");
                stringWriter.WriteLine("content1");
                while (task2flag != true && timeout.Elapsed < TimeSpan.FromSeconds(5))
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
                task2flag = true;
                stringWriter.WriteLine("content2");
                stringWriter.WriteLine("content2");
                stringWriter.WriteLine("content2");
                stringWriter.WriteLine("content2");
                return stringWriter.ToString();
            });

            var task2Output = task2.GetAwaiter().GetResult();
            var task1Output = task1.GetAwaiter().GetResult();

            // there was no output in the current task, the output should be empty
            var content = stringWriter.ToString();
            content.Should().BeNullOrWhiteSpace();

            // task1 and task2 should output into their respective buckets
            task1Output.Should().NotBeNullOrWhiteSpace();
            task2Output.Should().NotBeNullOrWhiteSpace();

            task1Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content1").And.HaveCount(8);
            task2Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Should().OnlyContain(i => i == "content2").And.HaveCount(8);
        }
    }

    [TestMethod]
    public void ThreadSafeStringWriterWritesLinesIntoDifferentWritesSeparately()
    {
        // Suppress the flow of parent context here becuase this test method will run in
        // a task already and we don't want the existing async context to interfere with this.
        using (ExecutionContext.SuppressFlow())
        {
            // The string writer mixes output captured by different instances if they are in the same taks, or under the same task context
            // and use the same output type. In the any of the "out" writers we should see all the output from the writers marked as "out"
            // and in any of the debug writers we should see all "debug" output.
            using var stringWriter1 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out");
            using var stringWriter2 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "debug");
            using var stringWriter3 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "out");
            using var stringWriter4 = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "debug");
            // Writing the data needs to run in a task, because that is how the writer is designed,
            // because we always run test in a task, so we must not setup the parent context, otherwise
            // it would capture output of all tests.
            var result = Task.Run(() =>
            {
                stringWriter1.WriteLine("out");
                stringWriter2.WriteLine("debug");

                Task.Run(() =>
                {
                    stringWriter3.WriteLine("out");
                    stringWriter4.WriteLine("debug");
                }).GetAwaiter().GetResult();

                return new { Out = stringWriter1.ToString(), Debug = stringWriter2.ToString() };
            }).GetAwaiter().GetResult();

            // task1 and task2 should output into their respective buckets
            result.Out.Should().NotBeNullOrWhiteSpace();
            result.Debug.Should().NotBeNullOrWhiteSpace();

            var output = result.Out.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            output.Should().OnlyContain(i => i == "out").And.HaveCount(2);

            var debug = result.Debug.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            debug.Should().OnlyContain(i => i == "debug").And.HaveCount(2);
        }
    }
}
