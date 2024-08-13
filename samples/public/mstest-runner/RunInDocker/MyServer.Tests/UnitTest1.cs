// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace MyServer.Tests;

[TestClass]
public class ServerManager
{
    public static Process? ServerProcess;

    [AssemblyInitialize]
    public static async Task StartServer(TestContext _)
    {
        var serverPath = Path.GetFullPath(Path.Combine("..", "..", "app", "MyServer.dll"));
        ServerProcess = Process.Start("dotnet", $"exec {serverPath}");
        // Give server time to spin up.
        await Task.Delay(1000);
    }

    [AssemblyCleanup]
    public static void StopServer()
    {
        ServerProcess?.Kill();
    }
}

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public async Task TestMethod1()
    {
        var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(2) };
        var response = await client.GetStringAsync("http://localhost:8080/hello");
        Assert.AreEqual("Hello, World!", response);
    }
}
