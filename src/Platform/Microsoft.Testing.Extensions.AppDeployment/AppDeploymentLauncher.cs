// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.AppDeployment;

/// <summary>
/// An <see cref="ITestHostLauncher"/> that deploys the test host payload into an isolated
/// directory and then launches it from there, instead of starting it in place.
/// </summary>
/// <remarks>
/// This demonstrates a launch mechanism that is more than a pass-through <c>Process.Start</c>: it
/// performs a real deployment side effect, and it returns a handle that intentionally does not
/// expose a local process id — exercising the platform's mechanism-agnostic monitoring path (the
/// same shape an AUMID-activated packaged app, a container, or a remote launch would use).
/// </remarks>
internal sealed class AppDeploymentLauncher : ITestHostLauncher
{
    public string Uid => nameof(AppDeploymentLauncher);

    public string Version => "1.0.0";

    public string DisplayName => "Application deployment launcher";

    public string Description => "Deploys the test host to an isolated directory and launches it from there.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // 1. Deploy: stage the whole test host payload into an isolated directory.
        string deploymentDirectory = Path.Combine(Path.GetTempPath(), "MTPAppDeployment", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDirectory, deploymentDirectory);

        // 2. Launch the deployed copy, forwarding the platform-prepared arguments and environment
        //    (which include the controller IPC pipe name the host connects back on).
        string deployedFileName = Path.Combine(deploymentDirectory, Path.GetFileName(context.FileName));
        var startInfo = new ProcessStartInfo(deployedFileName)
        {
            UseShellExecute = false,
            WorkingDirectory = deploymentDirectory,
        };

        foreach (string argument in context.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start deployed test host '{deployedFileName}'.");

        // Leave a breadcrumb next to the original app so the deployment is observable.
        File.WriteAllText(Path.Combine(sourceDirectory, "AppDeployment.txt"), deploymentDirectory);

        // 3. Return a handle that does NOT surface the underlying process id: the platform must rely
        //    purely on the lifecycle contract (WaitForExitAsync/ExitCode/HasExited/Exited/Terminate)
        //    and the IPC PID handshake, exactly as it would for a container/remote/AUMID launch.
        return Task.FromResult<ITestHostHandle>(new DeployedTestHostHandle(process));
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }
    }
}
