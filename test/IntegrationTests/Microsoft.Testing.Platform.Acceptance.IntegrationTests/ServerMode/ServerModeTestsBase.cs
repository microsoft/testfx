// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests.Messages.V100;

public partial /* for codegen regx */ class ServerModeTestsBase<TFixture> : AcceptanceTestBase<TFixture>
    where TFixture : TestAssetFixtureBase, new()
{
    private static readonly string Root = RootFinder.Find();
    private static readonly Dictionary<string, string?> DefaultEnvironmentVariables = new()
    {
        { "DOTNET_ROOT", $"{Root}/.dotnet" },
        { "DOTNET_INSTALL_DIR", $"{Root}/.dotnet" },
        { "DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1" },
        { "DOTNET_MULTILEVEL_LOOKUP", "0" },
    };

    protected async Task<TestingPlatformClient> StartAsServerAndConnectToTheClientAsync(TestHost testHost)
    {
        var environmentVariables = new Dictionary<string, string?>(DefaultEnvironmentVariables);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            string? key = entry.Key.ToString();
            if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables[key!] = entry.Value!.ToString()!;
        }

        // We expect to not fail for unhandled exception in server mode for IDE needs.
        environmentVariables.Add("TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION", "0");

        // To attach to the server on startup
        // environmentVariables.Add(EnvironmentVariableConstants.TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER, "1");
        TcpListener tcpListener = new(IPAddress.Loopback, 0);
        tcpListener.Start();
        StringBuilder builder = new();
        ProcessConfiguration processConfig = new(testHost.FullName)
        {
            OnStandardOutput = (_, output) => builder.AppendLine(CultureInfo.InvariantCulture, $"OnStandardOutput:\n{output}"),
            OnErrorOutput = (_, output) => builder.AppendLine(CultureInfo.InvariantCulture, $"OnErrorOutput:\n{output}"),
            OnExit = (processHandle, exitCode) => builder.AppendLine(CultureInfo.InvariantCulture, $"OnExit: exit code '{exitCode}'"),

            Arguments = $"--server --client-host localhost --client-port {((IPEndPoint)tcpListener.LocalEndpoint).Port} --diagnostic --diagnostic-verbosity trace",
            EnvironmentVariables = environmentVariables,
        };

        IProcessHandle processHandler = ProcessFactory.Start(processConfig, cleanDefaultEnvironmentVariableIfCustomAreProvided: false);

        TcpClient? tcpClient;
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(60));
        try
        {
            tcpClient = await tcpListener.AcceptTcpClientAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationTokenSource.Token)
        {
            throw new OperationCanceledException($"Timeout on connection for command line '{processConfig.FileName} {processConfig.Arguments}'\n{builder}", ex, cancellationTokenSource.Token);
        }

        return new TestingPlatformClient(new(tcpClient.GetStream()), tcpClient, processHandler);
    }
}
