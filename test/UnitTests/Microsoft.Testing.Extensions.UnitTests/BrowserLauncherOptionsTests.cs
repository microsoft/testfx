// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using Microsoft.Testing.Platform.Browser;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class BrowserLauncherOptionsTests
{
    private const string BootstrapToken = "secret-token";

    [TestMethod]
    public void Parse_ReadsDirectLauncherOptionsAndSdkResponseFile()
    {
        string responseFile = CreateResponseFile(
            """
            --server dotnettestcli
            --dotnet-test-transport http
            --dotnet-test-http-endpoint http://127.0.0.1:1234/
            --dotnet-test-http-token secret-token
            --filter FullyQualifiedName~Browser
            """);
        string browserExecutable = CreateEmptyFile();

        try
        {
            var options = BrowserLauncherOptions.Parse(
            [
                "--host-command-uri", Encode("dotnet"),
                "--host-arguments-uri", Encode("""run --property "value with spaces" """),
                "--host-working-directory-uri", Encode(Environment.CurrentDirectory),
                "--browser-executable-uri", Encode(browserExecutable),
                "--startup-timeout-seconds", "30",
                "--completion-timeout-seconds", "90",
                "--",
                $"@{responseFile}",
            ]);

            Assert.AreEqual("dotnet", options.HostCommand);
            Assert.AreEqual("""run --property "value with spaces" """, options.HostArguments);
            Assert.AreEqual(Path.GetFullPath(Environment.CurrentDirectory), options.HostWorkingDirectory);
            Assert.AreEqual(Path.GetFullPath(browserExecutable), options.BrowserExecutable);
            Assert.AreEqual(TimeSpan.FromSeconds(30), options.StartupTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(90), options.CompletionTimeout);
            Assert.Contains("--filter", options.TestApplicationArguments);
            Assert.AreEqual(new Uri("http://127.0.0.1:1234/"), options.Bootstrap.Endpoint);
            Assert.AreEqual(BootstrapToken, options.Bootstrap.Token);
        }
        finally
        {
            File.Delete(responseFile);
            File.Delete(browserExecutable);
        }
    }

    [TestMethod]
    public void Parse_PreservesEncodedNewlinesAndSpecialCharacters()
    {
        string responseFile = CreateResponseFile(
            """
            --server dotnettestcli
            --dotnet-test-transport http
            --dotnet-test-http-endpoint http://[::1]:1234/
            --dotnet-test-http-token secret-token
            """);
        string browserExecutable = CreateEmptyFile();
        const string hostArguments = "line1\r\nline2;%#'\"";

        try
        {
            var options = BrowserLauncherOptions.Parse(
            [
                "--host-command-uri", Encode("host"),
                "--host-arguments-uri", Encode(hostArguments),
                "--host-working-directory-uri", Encode(Environment.CurrentDirectory),
                "--browser-executable-uri", Encode(browserExecutable),
                "--startup-timeout-seconds", "1",
                "--completion-timeout-seconds", "2",
                "--",
                $"@{responseFile}",
            ]);

            Assert.AreEqual(hostArguments, options.HostArguments);
        }
        finally
        {
            File.Delete(responseFile);
            File.Delete(browserExecutable);
        }
    }

    [TestMethod]
    public void Parse_PreservesEmptyHostArguments()
    {
        string responseFile = CreateResponseFile(
            """
            --server dotnettestcli
            --dotnet-test-transport http
            --dotnet-test-http-endpoint http://127.0.0.1:1234/
            --dotnet-test-http-token secret-token
            """);
        string browserExecutable = CreateEmptyFile();

        try
        {
            var options = BrowserLauncherOptions.Parse(
            [
                "--host-command-uri", Encode("host"),
                "--host-arguments-uri", string.Empty,
                "--host-working-directory-uri", Encode(Environment.CurrentDirectory),
                "--browser-executable-uri", Encode(browserExecutable),
                "--startup-timeout-seconds", "1",
                "--completion-timeout-seconds", "2",
                "--",
                $"@{responseFile}",
            ]);

            Assert.AreEqual(string.Empty, options.HostArguments);
        }
        finally
        {
            File.Delete(responseFile);
            File.Delete(browserExecutable);
        }
    }

    [TestMethod]
    [DataRow("--server vstest", "--dotnet-test-transport http", "--dotnet-test-http-endpoint http://127.0.0.1:1234/", "--dotnet-test-http-token secret")]
    [DataRow("--server dotnettestcli", "--dotnet-test-transport pipe", "--dotnet-test-http-endpoint http://127.0.0.1:1234/", "--dotnet-test-http-token secret")]
    [DataRow("--server dotnettestcli", "--dotnet-test-transport http", "--dotnet-test-http-endpoint http://example.com/", "--dotnet-test-http-token secret")]
    [DataRow("--server dotnettestcli", "--dotnet-test-transport http", "--dotnet-test-http-endpoint http://127.0.0.1:1234/", "--dotnet-test-http-token secret token")]
    public void DotnetTestHttpBootstrap_RejectsInvalidBootstrap(
        string server,
        string transport,
        string endpoint,
        string token)
        => Assert.ThrowsExactly<BrowserLauncherException>(
            () => DotnetTestHttpBootstrap.Parse(
            [
                .. SplitPair(server),
                .. SplitPair(transport),
                .. SplitPair(endpoint),
                .. SplitPair(token),
            ]));

    [TestMethod]
    public void ResponseFileExpander_DoesNotLogSecretsInErrors()
    {
        string missingFile = Path.Combine(
            Path.GetTempPath(),
            $"missing-{BootstrapToken}-{Guid.NewGuid():N}.rsp");

        BrowserLauncherException exception = Assert.ThrowsExactly<BrowserLauncherException>(
            () => SdkResponseFileExpander.Expand([$"@{missingFile}"]));

        Assert.DoesNotContain(BootstrapToken, exception.Message);
        Assert.AreEqual("Unable to read the SDK response file.", exception.Message);
    }

    [TestMethod]
    public void HostProcess_ParsesOnlyExactHttpAppUrlReadiness()
    {
        Assert.AreEqual(
            new Uri("http://127.0.0.1:1234/"),
            HostProcess.TryParseAppUrl("App url: http://127.0.0.1:1234/"));
        Assert.IsNull(HostProcess.TryParseAppUrl("App url: https://127.0.0.1:1234/"));
        Assert.IsNull(HostProcess.TryParseAppUrl("Debug at url: http://127.0.0.1:1234/"));
        Assert.IsNull(HostProcess.TryParseAppUrl("Now listening on: http://127.0.0.1:1234/"));
        Assert.ThrowsExactly<BrowserLauncherException>(
            () => HostProcess.TryParseAppUrl("App url: http://example.com/"));
    }

    [TestMethod]
    public void DiagnosticBuffer_RedactsBootstrapValuesAndBoundsOutput()
    {
        var diagnostics = new DiagnosticBuffer(
            BootstrapToken,
            "abc",
            "http://127.0.0.1:1234/");

        diagnostics.Add("one", $"token={BootstrapToken}");
        diagnostics.Add("two", "http://127.0.0.1:1234/");
        diagnostics.Add("three", "short=abc");
        diagnostics.Add("four", new string('x', 5_000));
        string output = diagnostics.Format();

        Assert.DoesNotContain(BootstrapToken, output);
        Assert.DoesNotContain("abc", output);
        Assert.DoesNotContain("http://127.0.0.1:1234/", output);
        Assert.Contains("[redacted]", output);
        Assert.IsLessThan(4_500, output.Length);
    }

    [TestMethod]
    public void BrowserTerminalResult_FatalErrorIsReportedThroughDiagnostics()
    {
        var diagnostics = new DiagnosticBuffer("abc");
        var result = new BrowserTerminalResult(1, "bootstrap abc failed");

        BrowserLauncherException exception = Assert.ThrowsExactly<BrowserLauncherException>(
            () => result.GetExitCode(diagnostics));

        Assert.AreEqual("The browser supervisor reported a fatal error.", exception.Message);
        Assert.Contains("bootstrap [redacted] failed", diagnostics.Format());
    }

    [TestMethod]
    public async Task BrowserRunMonitor_CancellationStopsCompletionWaitPromptly()
    {
        using var cancellationTokenSource = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(100));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => BrowserRunMonitor.WaitAsync(
                WaitForCompletionAsync,
                WaitForExitAsync,
                WaitForExitAsync,
                cancellationTokenSource.Token));

        Assert.IsLessThan(TimeSpan.FromSeconds(5), stopwatch.Elapsed);

        static async Task<int> WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        static Task WaitForExitAsync(CancellationToken cancellationToken)
            => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static string[] SplitPair(string value)
    {
        int separator = value.IndexOf(' ');
        return [value[..separator], value[(separator + 1)..]];
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static string CreateResponseFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"mtp-browser-{Guid.NewGuid():N}.rsp");
        File.WriteAllText(path, content);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return path;
    }

    private static string CreateEmptyFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mtp-browser-{Guid.NewGuid():N}.exe");
        File.WriteAllText(path, string.Empty);
        return path;
    }
}
#endif
