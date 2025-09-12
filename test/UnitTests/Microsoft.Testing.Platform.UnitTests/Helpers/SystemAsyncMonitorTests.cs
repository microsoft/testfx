// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class SystemAsyncMonitorTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task AsyncMonitor_ShouldCorrectlyLock()
    {
        var asyncSystemMonitor = (SystemAsyncMonitor)new SystemMonitorAsyncFactory().Create();
        bool lockState = false;
        List<Task> tasks = [];
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(Task.Run(TestLock, TestContext.CancellationToken));
        }

        await Task.WhenAll([.. tasks]);

        // Give more time to be above 3s
        Thread.Sleep(500);

        Assert.IsGreaterThan(3000, stopwatch.ElapsedMilliseconds);

        async Task TestLock()
        {
            using (await asyncSystemMonitor.LockAsync(TimeSpan.FromSeconds(60)))
            {
                if (lockState)
                {
                    throw new InvalidOperationException("Expected lock state false");
                }

                lockState = true;
                await Task.Delay(1000, TestContext.CancellationToken);
                lockState = false;
            }
        }

        asyncSystemMonitor.Dispose();
    }
}
