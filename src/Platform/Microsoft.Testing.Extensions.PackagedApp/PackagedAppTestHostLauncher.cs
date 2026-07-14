// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostLauncher"/> for Windows test applications: it deploys a non-packaged
/// (loose-layout) test host (for example, unpackaged WinUI) into an isolated directory and launches
/// it from there, instead of starting the test host in place. Genuinely packaged (MSIX) layouts —
/// UWP or packaged WinUI — are rejected for now (see the remarks).
/// </summary>
/// <remarks>
/// <para>
/// Packaged Windows apps — produced by both UWP and packaged WinUI projects, and shipped as MSIX —
/// cannot be started with a plain <c>Process.Start</c> from the build output. They share the same
/// launch mechanism: the loose layout is registered with the <c>PackageManager</c> and the app is
/// activated by Application User Model ID (AUMID) via <c>IApplicationActivationManager</c>. That
/// mechanism is the reason VSTest's <c>UwpTestHostRuntimeProvider</c> exists (it serves both UWP and
/// WinUI); see https://github.com/microsoft/testfx/issues/9933.
/// </para>
/// <para>
/// This reference implementation performs the deploy-and-launch step for a <em>non-packaged</em>
/// (loose-layout) test host (stage the layout, then launch the produced executable from the
/// deployment directory). A genuinely packaged layout — detected by the presence of an
/// <c>AppxManifest.xml</c> — cannot be started with <c>Process.Start</c>; its AUMID-activation branch
/// (registering the layout with <c>PackageManager.RegisterPackageByUriAsync</c> and calling
/// <c>IApplicationActivationManager.ActivateApplication</c>) is a clearly-marked follow-up
/// (https://github.com/microsoft/testfx/issues/9933), so for now such a layout is rejected with an
/// actionable error rather than silently failing at launch. The AUMID that activation will use is
/// already computed from the manifest by <see cref="AppxManifestInfo"/>.
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

    // Packaged Windows apps (UWP/WinUI) are a Windows-only concept. On other operating systems the
    // launcher stays disabled so it is never registered, which keeps the platform on its default
    // in-process/Process.Start path instead of forcing the controller (deploy-and-launch) host.
    public Task<bool> IsEnabledAsync() => Task.FromResult(OperatingSystem.IsWindows());

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Honor immediate cancellation before doing any (potentially expensive) deployment work.
        cancellationToken.ThrowIfCancellationRequested();

        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // A genuinely packaged (MSIX) app cannot be started with Process.Start: it must be registered
        // with the PackageManager and activated by AUMID. That path is not implemented yet (see issue
        // #9933), so fail fast with an actionable message — including the AUMID activation would use —
        // instead of starting an executable that cannot host the run. The manifest lives at the package
        // layout root, which may be an ancestor of the executable's directory (Application/@Executable
        // can point into a subdirectory), so search upward rather than only the executable's directory.
        string? manifestPath = AppxManifestInfo.FindManifestPath(sourceDirectory);
        if (manifestPath is not null)
        {
            var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);

            // Resolve the application matching the executable the platform asked to launch so the
            // reported identity is the one activation would actually use (a package can declare several
            // applications). Fall back to the package family name when the manifest declares none.
            AppxApplicationInfo? application = manifestInfo.ResolveApplication(Path.GetFileName(context.FileName));
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ExtensionResources.PackagedAppLaunchNotSupported,
                    application?.AppUserModelId ?? manifestInfo.PackageFamilyName,
                    manifestPath));
        }

        // The layout is not packaged (no AppxManifest.xml). Deploy the loose layout into an isolated
        // directory and launch the produced executable from there.
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
