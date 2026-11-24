// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[TestClass]
public sealed class EndpointTests(AspNetCoreApplication application)
{
    [TestMethod]
    public async Task GreetingEndpointUsesHostConfiguration()
    {
        using HttpClient client = application.CreateClient();

        string greeting = await client.GetStringAsync("/greeting");

        Assert.AreEqual("Hello from ASP.NET Core", greeting);
    }
}
