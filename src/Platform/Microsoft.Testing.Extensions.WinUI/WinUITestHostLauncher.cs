// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.WinUI;

/// <summary>
/// An <see cref="ITestHostLauncher"/> for WinUI test applications: it deploys the test host output
/// (the app's loose layout) into an isolated directory and launches it from there, instead of
/// starting the test host in place.
/// </summary>
/// <remarks>
/// <para>
/// WinUI applications cannot always be started with a plain <c>Process.Start</c> from the build
/// output:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <b>Unpackaged WinUI</b> (the path implemented here): the app's loose layout is deployed to a
///     deployment directory and the produced executable is launched from there.
///     </description>
///   </item>
///   <item>
///     <description>
///     <b>Packaged WinUI / MSIX</b> (future work): the loose layout must be registered with the
///     <c>PackageManager</c> and the app activated by Application User Model ID (AUMID) via
///     <c>IApplicationActivationManager</c>. That step is the reason VSTest's
///     <c>UwpTestHostRuntimeProvider</c> exists; see https://github.com/microsoft/testfx/issues/2784.
///     The branch is left as a clearly-marked extension point below.
///     </description>
///   </item>
/// </list>
/// <para>
/// In both cases the platform owns argument/environment preparation, the controller-to-host IPC
/// pipe, the PID handshake, and the lifetime-handler dispatch; this launcher only performs the
/// deploy-and-create step and returns an <see cref="ITestHostHandle"/> the platform monitors.
/// </para>
/// </remarks>
internal sealed class WinUITestHostLauncher : ITestHostLauncher
{
    public string Uid => nameof(WinUITestHostLauncher);

    public string Version => "1.0.0";

    public string DisplayName => "WinUI test host launcher";

    public string Description => "Deploys a WinUI test host to an isolated directory and launches it from there.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // 1. Deploy the app's loose layout into an isolated directory. For packaged WinUI this is
        //    also where the layout would be registered via PackageManager.RegisterPackageByUriAsync.
        string deploymentDirectory = Path.Combine(Path.GetTempPath(), "MTPWinUIDeployment", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDirectory, deploymentDirectory);

        // 2. Launch the deployed test host, forwarding the platform-prepared arguments and
        //    environment (which include the controller IPC pipe name the host connects back on).
        //    Packaged WinUI would instead AUMID-activate here via IApplicationActivationManager and
        //    wrap the activated process id.
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
            ?? throw new InvalidOperationException($"Failed to start deployed WinUI test host '{deployedFileName}'.");

        // Leave a breadcrumb next to the original app so the deployment is observable by callers/tests.
        File.WriteAllText(Path.Combine(sourceDirectory, "WinUIDeployment.txt"), deploymentDirectory);

        // 3. Return a handle that deliberately does NOT surface the underlying process id, validating
        //    that the platform relies purely on the lifecycle contract
        //    (WaitForExitAsync/ExitCode/HasExited/Exited/Terminate) and the IPC PID handshake. This
        //    matches launch mechanisms where no local, query-able PID is available (e.g. an
        //    AppContainer-sandboxed activation surfaced through a broker).
        return Task.FromResult<ITestHostHandle>(new WinUITestHostHandle(process));
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
