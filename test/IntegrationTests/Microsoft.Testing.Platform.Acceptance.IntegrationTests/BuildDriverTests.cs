// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class BuildDriverTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task BuildCmd_PropagatesMSBuildFailureExitCode()
    {
        string directory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"build-driver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string project = Path.Combine(directory, "intentional-failure.proj");
        string binlog = Path.Combine(directory, "intentional-failure.binlog");
        File.WriteAllText(
            project,
            """
            <Project>
              <Target Name="Restore" />
              <Target Name="Build">
                <Error Text="Intentional build-driver failure." />
              </Target>
            </Project>
            """);

        string buildCommand = Path.Combine(RootFinder.Find(), "build.cmd");
        string command =
            $"& '{buildCommand}' -projects '{project}' -bln '{binlog}'; exit $LASTEXITCODE";
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            WorkingDirectory = RootFinder.Find(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new AssertFailedException("The build-driver test process did not start.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        await process.WaitForExitAsync(TestContext.CancellationToken);
        string output = await standardOutput + await standardError;

        Assert.AreNotEqual(0, process.ExitCode, output);
        Assert.Contains("Intentional build-driver failure.", output);
        Assert.IsTrue(File.Exists(binlog), $"The failing build did not create '{binlog}'.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task BuildCmd_ForwardsMultiThreadedSwitch()
    {
        string directory = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"build-driver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string project = Path.Combine(directory, "successful-build.proj");
        string binlog = Path.Combine(directory, "successful-build.binlog");
        File.WriteAllText(
            project,
            """
            <Project>
              <Target Name="Restore" />
              <Target Name="Build">
                <Message Importance="High" Text="Build driver accepted -mt 0." />
              </Target>
            </Project>
            """);

        string buildCommand = Path.Combine(RootFinder.Find(), "build.cmd");
        string command =
            $"& '{buildCommand}' -projects '{project}' -mt 0 -bln '{binlog}'; exit $LASTEXITCODE";
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
            WorkingDirectory = RootFinder.Find(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new AssertFailedException("The build-driver test process did not start.");

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
        await process.WaitForExitAsync(TestContext.CancellationToken);
        string output = await standardOutput + await standardError;

        Assert.AreEqual(0, process.ExitCode, output);
        Assert.Contains("Build driver accepted -mt 0.", output);
        Assert.IsTrue(File.Exists(binlog), $"The successful build did not create '{binlog}'.");
    }
}
