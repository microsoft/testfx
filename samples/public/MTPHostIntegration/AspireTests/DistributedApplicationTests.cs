// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Aspire.Hosting.Testing;

namespace MTPHostIntegration.AspireTests;

[TestClass]
public sealed class DistributedApplicationTests
{
    [TestMethod]
    public async Task ApiGreetingIsAvailable()
    {
        IDistributedApplicationTestingBuilder builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.AspireAppHost>();
        await using var app = await builder.BuildAsync();
        await app.StartAsync();
        using HttpClient client = app.CreateHttpClient("api");

        string greeting = await client.GetStringAsync("/greeting");

        Assert.AreEqual("Hello from Aspire", greeting);
    }
}
