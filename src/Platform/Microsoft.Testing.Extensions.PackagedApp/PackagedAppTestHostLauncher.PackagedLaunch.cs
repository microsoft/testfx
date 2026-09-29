// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class PackagedAppTestHostLauncher
{
    private static string? GetTargetExecutable(Func<string, string?> getEnvironmentVariable)
    {
        string? targetExecutable = getEnvironmentVariable(TargetExecutableEnvironmentVariable);
        return targetExecutable is null || targetExecutable.Trim().Length == 0
            ? null
            : Path.IsPathFullyQualified(targetExecutable)
                ? Path.GetFullPath(targetExecutable)
                : throw new InvalidOperationException(
                    $"Environment variable '{TargetExecutableEnvironmentVariable}' must contain a fully qualified executable path.");
    }

#if PACKAGEDAPP_WINRT
    private static string ResolveFinalPath(string path)
    {
        using Microsoft.Win32.SafeHandles.SafeFileHandle handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var buffer = new StringBuilder(32_768);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Failed to resolve the physical path for packaged test host '{path}'.");
        }

        const string DevicePathPrefix = @"\\?\";
        const string DeviceUncPathPrefix = @"\\?\UNC\";
        string finalPath = buffer.ToString();
        return finalPath.StartsWith(DeviceUncPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? @"\\" + finalPath[DeviceUncPathPrefix.Length..]
            : finalPath.StartsWith(DevicePathPrefix, StringComparison.Ordinal)
                ? finalPath[DevicePathPrefix.Length..]
                : finalPath;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        Microsoft.Win32.SafeHandles.SafeFileHandle file,
        StringBuilder filePath,
        uint filePathLength,
        uint flags);

    private static Task<ITestHostHandle> LaunchPackagedAsync(
        TestHostLaunchContext context,
        string manifestPath,
        string? appxRecipePath,
        CancellationToken cancellationToken)
        => PackageRegistrationLock.WithManifestAsync(
            manifestPath,
            manifestInfo => LaunchRegisteredPackagedAsync(context, manifestPath, appxRecipePath, manifestInfo, cancellationToken),
            cancellationToken);

    private static async Task<ITestHostHandle> LaunchRegisteredPackagedAsync(
        TestHostLaunchContext context,
        string manifestPath,
        string? appxRecipePath,
        AppxManifestInfo manifestInfo,
        CancellationToken cancellationToken)
    {
        // Resolve the application matching the executable the platform asked to launch so activation
        // targets the AUMID of the right app (a package can declare several applications). A package
        // that declares no application has no AUMID to activate.
        AppxApplicationInfo application = manifestInfo.ResolveApplication(Path.GetDirectoryName(manifestPath)!, context.FileName)
            ?? throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.PackagedAppNoApplicationToActivate,
                    manifestInfo.PackageFamilyName,
                    manifestPath));

        // Registration provisions the package-owned LocalState directory and its AppContainer ACL.
        // Handoffs must be written only after this completes; creating the directory from the unpackaged
        // controller first would give it the controller's ACL and make it unreadable by the activated app.
        await PackageDeployer.RegisterAsync(manifestPath, appxRecipePath, manifestInfo, cancellationToken).ConfigureAwait(false);

        // Hand off the explicit safe environment allowlist through package LocalState. Controller-host runs
        // key the file by controller PID; retry runs key it by a hash of their unique pipe name. An
        // AUMID-activated process does not inherit the controller's environment, so the activated host
        // applies these values before platform and extension connect-back initialization.
        string? handshakeId = PackagedAppConnectBackHandshake.TryGetHandshakeId(context.Arguments);
        string? handshakePath = null;
        string? activationPayloadPath = null;
        string? scratchDirectory = null;
        string? resultsScratchDirectory = null;
        string? resultsRecoveryDirectory = null;
        string? diagnosticScratchDirectory = null;
        string? diagnosticRecoveryDirectory = null;
        string? retryArtifactManifestPath = null;
        string? retryArtifactManifestDestinationPath = null;
        bool isRetryChild = PackagedAppConnectBackHandshake.TryGetTestHostControllerPid(context.Arguments) is null
            && handshakeId is not null;
        try
        {
            IReadOnlyList<string> hostArguments = context.Arguments;
            if (application.RunsInAppContainer)
            {
                resultsRecoveryDirectory = GetControllerPath(
                    TryGetOptionValue(context.Arguments, ResultsDirectoryOption) ?? "TestResults",
                    context);
                string diagnosticOutputDirectory = GetControllerPath(
                    TryGetOptionValue(context.Arguments, DiagnosticOutputDirectoryOption)
                        ?? resultsRecoveryDirectory,
                    context);
                diagnosticRecoveryDirectory = Path.Combine(diagnosticOutputDirectory, "AppContainer");
                scratchDirectory = Path.Combine(
                    PackagedAppConnectBackHandshake.GetHandshakeDirectory(manifestInfo.PackageFamilyName),
                    "MtpTestHost",
                    handshakeId ?? Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(scratchDirectory);
                resultsScratchDirectory = Path.Combine(scratchDirectory, "results");
                diagnosticScratchDirectory = Path.Combine(scratchDirectory, "diagnostics");
                Directory.CreateDirectory(resultsScratchDirectory);
                Directory.CreateDirectory(diagnosticScratchDirectory);
                Environment.SetEnvironmentVariable(AppContainerArtifactRootsConfiguredEnvironmentVariableName, "1");
                Environment.SetEnvironmentVariable(ArtifactPathDestinationRootEnvironmentVariableName, resultsRecoveryDirectory);
                Environment.SetEnvironmentVariable(DiagnosticArtifactPathDestinationRootEnvironmentVariableName, diagnosticRecoveryDirectory);
                hostArguments = RedirectAppContainerFileSystemOptions(
                    context.Arguments,
                    resultsScratchDirectory,
                    diagnosticScratchDirectory,
                    removeMSBuildNode: isRetryChild);
                retryArtifactManifestDestinationPath = context.EnvironmentVariables
                    .FirstOrDefault(environmentVariable => string.Equals(
                        environmentVariable.Key,
                        RetryRecoveredArtifactManifestEnvironmentVariableName,
                        StringComparison.OrdinalIgnoreCase))
                    .Value;
                if (retryArtifactManifestDestinationPath is { Length: > 0 })
                {
                    retryArtifactManifestPath = Path.Combine(scratchDirectory, "retry-recovered-artifacts.manifest");
                }
            }

            if (handshakeId is not null)
            {
                handshakePath = PackagedAppConnectBackHandshake.GetHandshakeFilePath(manifestInfo.PackageFamilyName, handshakeId);
                PackagedAppConnectBackHandshake.Write(
                    handshakePath,
                    GetConnectBackEnvironment(context, retryArtifactManifestPath)
                        .Append(new(LauncherModeEnvironmentVariable, NeverMode))
                        .Append(new(ArtifactPathSourceRootEnvironmentVariableName, resultsScratchDirectory))
                        .Append(new(ArtifactPathDestinationRootEnvironmentVariableName, resultsRecoveryDirectory))
                        .Append(new(DiagnosticArtifactPathSourceRootEnvironmentVariableName, diagnosticScratchDirectory))
                        .Append(new(DiagnosticArtifactPathDestinationRootEnvironmentVariableName, diagnosticRecoveryDirectory)));
            }

            PackagedAppActivationData activationData = CreateActivationArguments(
                application,
                hostArguments,
                PackagedAppConnectBackHandshake.GetHandshakeDirectory(manifestInfo.PackageFamilyName));
            string activationArguments = activationData.Arguments;
            activationPayloadPath = activationData.PayloadPath;

            cancellationToken.ThrowIfCancellationRequested();
            uint processId = PackageDeployer.Activate(application.AppUserModelId, activationArguments);

            // The handle owns deleting the hand-off from now on: the activated host normally consumes and
            // deletes it, but if that host exits before reading it the handle still removes it on dispose,
            // so connect-back data is never left behind.
            return new ActivatedAppTestHostHandle(
                processId,
                handshakePath,
                activationPayloadPath,
                scratchDirectory,
                resultsScratchDirectory,
                resultsRecoveryDirectory,
                diagnosticScratchDirectory,
                diagnosticRecoveryDirectory,
                retryArtifactManifestPath,
                retryArtifactManifestDestinationPath);
        }
        catch
        {
            // No host was activated to consume the hand-off, so remove it now; leaving it behind would
            // let a later run — or a process that reuses this controller PID — pick up stale connect-back
            // data.
            if (handshakePath is not null)
            {
                PackagedAppConnectBackHandshake.TryDelete(handshakePath);
            }

            PackagedAppActivationArguments.TryDeletePayload(activationPayloadPath);
            TryDeleteScratchDirectory(scratchDirectory);
            throw;
        }
    }

#else
    private static Task<ITestHostHandle> LaunchPackagedAsync(
        TestHostLaunchContext context,
        string manifestPath,
        string? appxRecipePath,
        CancellationToken cancellationToken)
    {
        // Registering and activating a packaged (MSIX) app needs the PackageManager WinRT projection,
        // which is only available in the Windows build of this extension. When a consumer resolves the
        // plain net8.0/net9.0 build, fail fast with an actionable message — including the AUMID that
        // activation would use — pointing at the Windows TFM, instead of starting an executable that
        // cannot host the run.
        _ = cancellationToken;
        _ = appxRecipePath;
        var manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);
        AppxApplicationInfo? application = manifestInfo.ResolveApplication(Path.GetDirectoryName(manifestPath)!, context.FileName);
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ExtensionResources.PackagedAppLaunchNotSupported,
                application?.AppUserModelId ?? manifestInfo.PackageFamilyName,
                manifestPath));
    }
#endif
}
