// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class SystemAsyncMonitorTests : TestBase
{
    public SystemAsyncMonitorTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    public async Task AsyncMonitor_ShouldCorrectlyLock()
    {
        var asyncSystemMonitor = (SystemAsyncMonitor)new SystemMonitorAsyncFactory().Create();
        bool lockState = false;
        List<Task> tasks = [];
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(() => TestLock()));
        }

        await Task.WhenAll(tasks.ToArray());

        // Give more time to be above 3s
        Thread.Sleep(500);

        Assert.IsTrue(stopwatch.ElapsedMilliseconds > 3000);

        async Task TestLock()
        {
            using (await asyncSystemMonitor.LockAsync(TimeSpan.FromSeconds(60)))
            {
                if (lockState)
                {
                    throw new InvalidOperationException("Expected lock state false");
                }

                lockState = true;
                await Task.Delay(1000);
                lockState = false;
            }
        }

        asyncSystemMonitor.Dispose();
    }
}
