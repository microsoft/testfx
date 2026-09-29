// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class PackagedAppTestHostLauncher
{
    private static ITestHostHandle LaunchLooseLayout(TestHostLaunchContext context, string sourceDirectory, CancellationToken cancellationToken)
    {
        // 1. Copy the app's loose layout into an isolated directory.
        string deploymentDirectory = Path.Combine(Path.GetTempPath(), "MTPPackagedAppDeployment", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDirectory, deploymentDirectory, cancellationToken);

        // 2. Launch the deployed test host, forwarding the platform-prepared arguments and
        //    environment (which include the controller IPC pipe name the host connects back on).
        string deployedFileName = Path.Combine(deploymentDirectory, Path.GetFileName(context.FileName));
        var startInfo = new ProcessStartInfo(deployedFileName)
        {
            UseShellExecute = false,
            // Honor an explicitly requested working directory; otherwise run from the deployment dir.
            WorkingDirectory = context.WorkingDirectory ?? deploymentDirectory,
        };

        foreach (string argument in context.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        // The deployed child is the retry attempt itself. Prevent it from enabling this launcher again and
        // recursively creating another controller/deployment layer when the parent was opted in with "always".
        startInfo.Environment[LauncherModeEnvironmentVariable] = NeverMode;

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start deployed packaged-app test host '{deployedFileName}'.");

        // 3. Return a handle that deliberately does NOT surface the underlying process id, validating
        //    that the platform relies purely on the lifecycle contract
        //    (WaitForExitAsync/ExitCode/HasExited/Terminate) and the IPC PID handshake. The handle also
        //    owns cleanup of the deployment directory once the host has exited.
        return new PackagedAppTestHostHandle(process, deploymentDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.EnumerateFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)), cancellationToken);
        }
    }
}
