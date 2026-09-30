// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Aspire.Hosting.Testing;

namespace MTPHostIntegration.AspireTests;

[TestClass]
public sealed class DistributedApplicationTests(HostedTestMetadata metadata)
{
    private const int StartupTimeoutMilliseconds = 120_000;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(StartupTimeoutMilliseconds, CooperativeCancellation = true)]
    public async Task ApiGreetingIsAvailable()
    {
        Assert.AreEqual("Aspire", metadata.HostingModel);
        CancellationToken cancellationToken = TestContext.CancellationToken;
        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAppHost>(cancellationToken: cancellationToken);
        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken);
        using HttpClient client = app.CreateHttpClient("api");

        string greeting = await client.GetStringAsync("/greeting", cancellationToken);

        Assert.AreEqual("Hello from Aspire", greeting);
    }
}
