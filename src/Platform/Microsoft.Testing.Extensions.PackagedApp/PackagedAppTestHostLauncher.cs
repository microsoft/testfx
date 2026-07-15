// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostLauncher"/> for Windows test applications. It handles two layouts:
/// a non-packaged (loose-layout) host — for example unpackaged WinUI — which is deployed into an
/// isolated directory and launched from there; and a genuinely packaged (MSIX) host — UWP or packaged
/// WinUI — which cannot be started with <c>Process.Start</c> and is instead registered with the OS and
/// activated by Application User Model ID (AUMID).
/// </summary>
/// <remarks>
/// <para>
/// A packaged Windows app cannot be started with a plain <c>Process.Start</c> from the build output.
/// It must be registered with the <c>PackageManager</c> and activated by AUMID via
/// <c>IApplicationActivationManager</c>. That is the mechanism VSTest's <c>UwpTestHostRuntimeProvider</c>
/// implements on top of Visual-Studio-internal deployment components; this extension implements the
/// equivalent using only public, redistributable Windows APIs (see
/// https://github.com/microsoft/testfx/issues/9933).
/// </para>
/// <para>
/// Because an AUMID-activated process is created by the Windows activation/PLM infrastructure rather
/// than by the controller, it does not inherit the controller-to-host connect-back environment
/// variables the platform prepared. The launcher hands those off out-of-band through the package's own
/// writable data folder (see <see cref="PackagedAppConnectBackHandshake"/>) and forwards the
/// platform-prepared command line as the activation arguments, which a packaged full-trust desktop app
/// receives as <c>argv</c>.
/// </para>
/// <para>
/// The full register-and-activate path ships only in the Windows build of this extension
/// (<c>net*-windows10.0.19041.0</c>), where the <c>PackageManager</c> WinRT projection is available.
/// The plain <c>net8.0</c>/<c>net9.0</c> build still deploys and launches non-packaged loose layouts,
/// but rejects a packaged layout with an actionable error so a consumer that resolves that build is
/// told to target a Windows TFM. Registering an unsigned build-output layout additionally requires
/// Developer Mode (or sideloading) to be enabled on the machine.
/// </para>
/// <para>
/// In all cases the platform owns argument/environment preparation, the controller-to-host IPC pipe,
/// the PID handshake, and the lifetime-handler dispatch; this launcher only performs the
/// deploy/register-and-create step and returns an <see cref="ITestHostHandle"/> the platform monitors.
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

    public async Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Honor immediate cancellation before doing any (potentially expensive) deployment work.
        cancellationToken.ThrowIfCancellationRequested();

        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // A packaged (MSIX) app is detected by the presence of an AppxManifest.xml. The manifest lives
        // at the package layout root, which may be an ancestor of the executable's directory
        // (Application/@Executable can point into a subdirectory), so search upward rather than only the
        // executable's directory.
        string? manifestPath = AppxManifestInfo.FindManifestPath(sourceDirectory);
        if (manifestPath is not null)
        {
            return await LaunchPackagedAsync(context, manifestPath, cancellationToken).ConfigureAwait(false);
        }

        // The layout is not packaged (no AppxManifest.xml). Deploy the loose layout into an isolated
        // directory and launch the produced executable from there.
        return LaunchLooseLayout(context, sourceDirectory, cancellationToken);
    }

#if PACKAGEDAPP_WINRT
    private static async Task<ITestHostHandle> LaunchPackagedAsync(TestHostLaunchContext context, string manifestPath, CancellationToken cancellationToken)
    {
        var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);

        // Resolve the application matching the executable the platform asked to launch so activation
        // targets the AUMID of the right app (a package can declare several applications). A package
        // that declares no application has no AUMID to activate.
        AppxApplicationInfo application = manifestInfo.ResolveApplication(Path.GetFileName(context.FileName))
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.PackagedAppNoApplicationToActivate,
                    manifestInfo.PackageFamilyName,
                    manifestPath));

        // Hand off the environment the platform prepared for the test host through the package's
        // LocalState, keyed by the test host controller PID so concurrent runs of the same package do
        // not collide. An AUMID-activated process is created by the Windows activation infrastructure and
        // does not inherit the controller's process environment (which a plain Process.Start child would),
        // so the full prepared environment — not just the connect-back additions — must be reproduced.
        // The activated host applies it in-process before the platform's environment-variable-based
        // connect-back runs (see PackagedAppConnectBackReader).
        string? testHostControllerPid = PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(context.Arguments);
        string? handshakePath = null;
        if (testHostControllerPid is not null)
        {
            handshakePath = PackagedAppConnectBackHandshake.GetHandshakeFilePath(manifestInfo.PackageFamilyName, testHostControllerPid);
            PackagedAppConnectBackHandshake.Write(handshakePath, context.EnvironmentVariables);
        }

        // Register the loose layout in place and activate it, forwarding the platform-prepared command
        // line (which a packaged full-trust desktop app receives as argv). Activation returns the real
        // process id, so the handle can monitor and terminate the actual activated process.
        string activationArguments = BuildCommandLine(context.Arguments);
        try
        {
            uint processId = await PackageDeployer
                .RegisterAndActivateAsync(manifestPath, application.AppUserModelId, activationArguments, cancellationToken)
                .ConfigureAwait(false);

            return new ActivatedAppTestHostHandle(processId);
        }
        catch
        {
            // No host was activated to consume the hand-off, so remove it now; leaving it behind would
            // let a later run — or a process that reuses this controller PID — pick up stale connect-back
            // data. On success the activated host owns deleting the file once it has read it.
            if (handshakePath is not null)
            {
                PackagedAppConnectBackHandshake.TryDelete(handshakePath);
            }

            throw;
        }
    }

    // Reconstructs a single command line from the platform-prepared arguments using the Windows
    // CommandLineToArgvW quoting rules, so the activated app parses back exactly the same argv.
    private static string BuildCommandLine(IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (string argument in arguments)
        {
            AppendArgument(builder, argument);
        }

        return builder.ToString();
    }

    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/PasteArguments.cs
    private static void AppendArgument(StringBuilder builder, string argument)
    {
        const char Quote = '"';
        const char Backslash = '\\';

        if (builder.Length != 0)
        {
            builder.Append(' ');
        }

        if (argument.Length != 0 && !ContainsWhitespaceOrQuote(argument))
        {
            builder.Append(argument);
            return;
        }

        builder.Append(Quote);
        int index = 0;
        while (index < argument.Length)
        {
            char c = argument[index++];
            if (c == Backslash)
            {
                int backslashCount = 1;
                while (index < argument.Length && argument[index] == Backslash)
                {
                    index++;
                    backslashCount++;
                }

                if (index == argument.Length)
                {
                    builder.Append(Backslash, backslashCount * 2);
                }
                else if (argument[index] == Quote)
                {
                    builder.Append(Backslash, (backslashCount * 2) + 1);
                    builder.Append(Quote);
                    index++;
                }
                else
                {
                    builder.Append(Backslash, backslashCount);
                }

                continue;
            }

            if (c == Quote)
            {
                builder.Append(Backslash);
                builder.Append(Quote);
                continue;
            }

            builder.Append(c);
        }

        builder.Append(Quote);
    }

    private static bool ContainsWhitespaceOrQuote(string value)
    {
        foreach (char c in value)
        {
            if (char.IsWhiteSpace(c) || c == '"')
            {
                return true;
            }
        }

        return false;
    }
#else
    private static Task<ITestHostHandle> LaunchPackagedAsync(TestHostLaunchContext context, string manifestPath, CancellationToken cancellationToken)
    {
        // Registering and activating a packaged (MSIX) app needs the PackageManager WinRT projection,
        // which is only available in the Windows build of this extension. When a consumer resolves the
        // plain net8.0/net9.0 build, fail fast with an actionable message — including the AUMID that
        // activation would use — pointing at the Windows TFM, instead of starting an executable that
        // cannot host the run.
        _ = cancellationToken;
        var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);
        AppxApplicationInfo? application = manifestInfo.ResolveApplication(Path.GetFileName(context.FileName));
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ExtensionResources.PackagedAppLaunchNotSupported,
                application?.AppUserModelId ?? manifestInfo.PackageFamilyName,
                manifestPath));
    }
#endif

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
