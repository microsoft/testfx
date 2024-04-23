// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class ThreadSafeStringWriterTests : TestContainer
{
    private bool _task2flag;

    public void ThreadSafeStringWriterWritesLinesFromDifferentTasksSeparately()
    {
        // Suppress the flow of parent context here because this test method will run in
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
                while (!_task2flag && timeout.Elapsed < TimeSpan.FromSeconds(5))
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
                _task2flag = true;
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
            Verify(string.IsNullOrWhiteSpace(content));

            // task1 and task2 should output into their respective buckets
            Verify(!string.IsNullOrWhiteSpace(task1Output));
            Verify(!string.IsNullOrWhiteSpace(task2Output));

            var task1Split = task1Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Verify(task1Split.SequenceEqual(Enumerable.Repeat("content1", 8)));
            var task2Split = task2Output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Verify(task2Split.SequenceEqual(Enumerable.Repeat("content2", 8)));
        }
    }

    public void ThreadSafeStringWriterWritesLinesIntoDifferentWritesSeparately()
    {
        // Suppress the flow of parent context here because this test method will run in
        // a task already and we don't want the existing async context to interfere with this.
        using (ExecutionContext.SuppressFlow())
        {
            // The string writer mixes output captured by different instances if they are in the same task, or under the same task context
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
            Verify(!string.IsNullOrWhiteSpace(result.Out));
            Verify(!string.IsNullOrWhiteSpace(result.Debug));

            var output = result.Out.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Verify(output.SequenceEqual(new[] { "out", "out" }));

            var debug = result.Debug.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            Verify(debug.SequenceEqual(new[] { "debug", "debug" }));
        }
    }
}
