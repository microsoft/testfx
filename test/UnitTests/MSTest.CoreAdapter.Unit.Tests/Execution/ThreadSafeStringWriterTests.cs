// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using TestFramework.ForTestingMSTest;

public class ThreadSafeStringWriterTests : TestContainer
{
    private bool _task2flag;

    public void ThreadSafeStringWriterWriteLineHasContentFromMultipleThreads()
    {
        using (ExecutionContext.SuppressFlow())
        {
            using var stringWriter = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "tst");
            void action(string x)
            {
                var count = 10;
                for (var i = 0; i < count; i++)
                {
                    // Choose WriteLine since it calls the entire sequence:
                    // Write(string) -> Write(char[]) -> Write(char)
                    stringWriter.WriteLine(x);
                }
            }

            var task1 = Task.Run(() =>
            {
                var timeout = Stopwatch.StartNew();
                action("content1");
                action("content1");
                action("content1");
                action("content1");
                while (_task2flag != true && timeout.Elapsed < TimeSpan.FromSeconds(5))
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
                _task2flag = true;
                action("content2");
                action("content2");
                action("content2");
                action("content2");
            });

            task2.GetAwaiter().GetResult();
            task1.GetAwaiter().GetResult();

            var content = stringWriter.ToString();
            Verify(!string.IsNullOrEmpty(content));

            // Validate that only whole lines are written, not a mix of random chars
            var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Verify(lines.Length > 0);
            foreach (var line in lines)
            {
                Verify(line.Equals("content1") || line.Equals("content2"));
            }
        }
    }
}
