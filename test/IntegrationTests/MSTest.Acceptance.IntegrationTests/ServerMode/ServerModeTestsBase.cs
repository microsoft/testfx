// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.ServerMode.IntegrationTests.Messages.V100;

namespace MSTest.Acceptance.IntegrationTests.Messages.V100;

public partial /* for codegen regx */ class ServerModeTestsBase : AcceptanceTestBase
{
    private static readonly string Root = RootFinder.Find();
    private static readonly Dictionary<string, string> DefaultEnvironmentVariables = new()
    {
        { "DOTNET_ROOT", $"{Root}/.dotnet" },
        { "DOTNET_INSTALL_DIR", $"{Root}/.dotnet" },
        { "DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1" },
        { "DOTNET_MULTILEVEL_LOOKUP", "0" },
    };

    protected ServerModeTestsBase(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    protected async Task<TestingPlatformClient> StartAsServerAndConnectToTheClientAsync(TestHost testHost)
    {
        var environmentVariables = new Dictionary<string, string>(DefaultEnvironmentVariables);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables[entry.Key.ToString()!] = entry.Value!.ToString()!;
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

    protected async Task<TestingPlatformClient> StartAsServerAndConnectAsync(TestHost testHost, bool enableDiagnostic = false)
    {
        var environmentVariables = new Dictionary<string, string>(DefaultEnvironmentVariables);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            // Skip all unwanted environment variables.
            if (WellKnownEnvironmentVariables.ToSkipEnvironmentVariables.Contains(entry.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            environmentVariables[entry.Key.ToString()!] = entry.Value!.ToString()!;
        }

        // We expect to not fail for unhandled exception in server mode for IDE needs.
        environmentVariables.Add("TESTINGPLATFORM_EXIT_PROCESS_ON_UNHANDLED_EXCEPTION", "0");

        // To attach to the server on startup
        // environmentVariables.Add(EnvironmentVariableConstants.TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER, "1");
        TaskCompletionSource<int> portFound = new();
        ProcessConfiguration processConfig = new(testHost.FullName)
        {
            OnStandardOutput =
            (_, output) =>
            {
                Match m = ParsePort().Match(output);
                if (m.Success && int.TryParse(m.Groups[1].Value, out int port))
                {
                    portFound.SetResult(port);
                }

                // Do not remove pls
                // NotepadWindow.WriteLine($"[OnStandardOutput] {output}");
            },

            // Do not remove pls
            // OnErrorOutput = (_, output) => NotepadWindow.WriteLine($"[OnErrorOutput] {output}"),
            OnErrorOutput = (_, output) =>
            {
                if (!portFound.Task.IsCompleted)
                {
                    try
                    {
                        portFound.SetException(new InvalidOperationException(output));
                    }
                    catch (InvalidOperationException)
                    {
                        // possible race
                    }
                }
            },
            OnExit = (processHandle, exitCode) =>
            {
                if (exitCode != 0)
                {
                    if (portFound.Task.Exception is null && !portFound.Task.IsCompleted)
                    {
                        portFound.SetException(new InvalidOperationException($"Port not found during parsing and process exited with code '{exitCode}'"));
                    }
                }
            },

            // OnExit = (_, exitCode) => NotepadWindow.WriteLine($"[OnExit] Process exit code '{exitCode}'"),
            Arguments = "--server --diagnostic --diagnostic-verbosity trace",
            EnvironmentVariables = environmentVariables,
        };

        IProcessHandle processHandler = ProcessFactory.Start(processConfig, cleanDefaultEnvironmentVariableIfCustomAreProvided: false);
        await portFound.Task;

        var tcpClient = new TcpClient();
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(90));
#pragma warning disable VSTHRD103 // Call async methods when in an async method
        await tcpClient.ConnectAsync(new IPEndPoint(IPAddress.Loopback, portFound.Task.Result), cancellationTokenSource.Token);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
        return new TestingPlatformClient(new(tcpClient.GetStream()), tcpClient, processHandler, enableDiagnostic);
    }

    [GeneratedRegex(@"Starting server. Listening on port '(\d+)'")]
    private static partial Regex ParsePort();
}
