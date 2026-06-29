// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostLauncher"/> for packaged Windows test applications (UWP and packaged
/// WinUI): it deploys the test host's loose layout into an isolated directory and launches it from
/// there, instead of starting the test host in place.
/// </summary>
/// <remarks>
/// <para>
/// Packaged Windows apps — produced by both UWP and packaged WinUI projects, and shipped as MSIX —
/// cannot be started with a plain <c>Process.Start</c> from the build output. They share the same
/// launch mechanism: the loose layout is registered with the <c>PackageManager</c> and the app is
/// activated by Application User Model ID (AUMID) via <c>IApplicationActivationManager</c>. That
/// mechanism is the reason VSTest's <c>UwpTestHostRuntimeProvider</c> exists (it serves both UWP and
/// WinUI); see https://github.com/microsoft/testfx/issues/2784.
/// </para>
/// <para>
/// This reference implementation performs the deploy-and-launch step (stage the loose layout, then
/// launch the produced executable from the deployment directory). The packaged AUMID-activation
/// branch — registering the layout with <c>PackageManager.RegisterPackageByUriAsync</c> and calling
/// <c>IApplicationActivationManager.ActivateApplication</c> — is a clearly-marked follow-up.
/// </para>
/// <para>
/// In all cases the platform owns argument/environment preparation, the controller-to-host IPC pipe,
/// the PID handshake, and the lifetime-handler dispatch; this launcher only performs the
/// deploy-and-create step and returns an <see cref="ITestHostHandle"/> the platform monitors.
/// </para>
/// </remarks>
internal sealed class PackagedAppTestHostLauncher : ITestHostLauncher
{
    public string Uid => nameof(PackagedAppTestHostLauncher);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.PackagedAppExtensionDisplayName;

    public string Description => ExtensionResources.PackagedAppExtensionDescription;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Honor immediate cancellation before doing any (potentially expensive) deployment work.
        cancellationToken.ThrowIfCancellationRequested();

        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // 1. Deploy the app's loose layout into an isolated directory. For packaged UWP/WinUI this is
        //    also where the layout would be registered via PackageManager.RegisterPackageByUriAsync.
        string deploymentDirectory = Path.Combine(Path.GetTempPath(), "MTPPackagedAppDeployment", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDirectory, deploymentDirectory, cancellationToken);

        // 2. Launch the deployed test host, forwarding the platform-prepared arguments and
        //    environment (which include the controller IPC pipe name the host connects back on).
        //    Packaged UWP/WinUI would instead AUMID-activate here via IApplicationActivationManager
        //    and wrap the activated process id.
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

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start deployed packaged-app test host '{deployedFileName}'.");

        // 3. Return a handle that deliberately does NOT surface the underlying process id, validating
        //    that the platform relies purely on the lifecycle contract
        //    (WaitForExitAsync/ExitCode/HasExited/Terminate) and the IPC PID handshake. This
        //    matches launch mechanisms where no local, queryable PID is available (e.g. an
        //    AppContainer-sandboxed AUMID activation surfaced through a broker). The handle also owns
        //    cleanup of the deployment directory once the host has exited.
        return Task.FromResult<ITestHostHandle>(new PackagedAppTestHostHandle(process, deploymentDirectory));
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
