// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class PackagedAppTestHostLauncher
{
    internal static PackagedAppActivationData CreateActivationArguments(
        AppxApplicationInfo application,
        IReadOnlyList<string> arguments,
        string localStateDirectory)
    {
        if (application.UsesLaunchActivationArguments)
        {
            // windowsApp/UWP activation exposes one opaque string through OnLaunched rather than argv.
            // Inline the compact versioned payload when it fits the documented launch-argument envelope;
            // otherwise spill only authenticated ciphertext to LocalState and carry its one-shot key in
            // the activation string. User filters/runsettings are therefore never persisted in plaintext.
            return PackagedAppActivationArguments.Create(arguments, localStateDirectory);
        }

        // A packaged full-trust desktop app receives activation arguments as process argv. Preserve the
        // existing Windows command-line quoting exactly for that path.
        var commandLineBuilder = new StringBuilder();
        foreach (string argument in arguments)
        {
            PasteArguments.AppendArgument(commandLineBuilder, argument);
        }

        return new PackagedAppActivationData(commandLineBuilder.ToString(), payloadPath: null);
    }

    /// <summary>
    /// Returns the AppContainer SID of the packaged application about to be launched, so the platform can
    /// authorize it on the controller-to-host IPC pipe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The platform creates that pipe before the launcher runs — it has to be listening before the host
    /// starts — so the package identity cannot be contributed from <see cref="LaunchTestHostAsync"/>. This
    /// method therefore re-derives it from the same layout <see cref="IsEnabledAsync"/> probes: the
    /// controller process <em>is</em> the test application whose host is about to be launched, so its base
    /// directory is that layout.
    /// </para>
    /// <para>
    /// The grant is deliberately narrow. Nothing is requested unless the selected test host application is
    /// packaged (MSIX) and runs inside an AppContainer, as classified by
    /// <see cref="AppxApplicationInfo.RunsInAppContainer"/>. A packaged
    /// full-trust desktop host already reaches the pipe with the platform's normal current-user protection
    /// and gets nothing extra — even when an AppContainer sibling shares its package. The value returned is
    /// the SID of this very package, derived from its own family name, so it cannot authorize any other
    /// package. The platform independently re-validates it and rejects anything that is not a specific
    /// AppContainer SID.
    /// </para>
    /// </remarks>
    /// <param name="testHostFileName">The executable path of the test host application being launched.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The AppContainer SIDs to authorize, which is at most this package's own SID.</returns>
    public Task<IReadOnlyList<string>> GetAuthorizedSecurityIdentitiesAsync(string testHostFileName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetAuthorizedAppContainerSecurityIdentifiers(_targetExecutable ?? testHostFileName));
    }

    private IReadOnlyList<string> GetAuthorizedAppContainerSecurityIdentifiers(string testHostFileName)
    {
        // AppContainers, package SIDs and named-pipe DACLs are Windows concepts.
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        string? mode = _getEnvironmentVariable(PipeAuthorizationModeEnvironmentVariable)?.Trim();
        if (string.Equals(mode, NeverMode, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // Only a packaged layout has a package identity to authorize. Resolve the manifest against the
        // selected executable, not merely the controller's base directory: one package can declare a
        // full-trust test host alongside an AppContainer sibling, and the sibling must not widen that run.
        string? sourceDirectory = Path.GetDirectoryName(testHostFileName);
        if (sourceDirectory is null
            || AppxManifestInfo.FindManifestPath(sourceDirectory, testHostFileName) is not { } manifestPath)
        {
            return [];
        }

        AppxManifestInfo manifestInfo;
        AppxApplicationInfo? application;
        try
        {
            manifestInfo = AppxManifestInfo.ReadFromManifest(manifestPath);
            application = manifestInfo.ResolveApplication(Path.GetDirectoryName(manifestPath)!, testHostFileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or XmlException)
        {
            // A manifest we cannot read is handled by the launch path, which reports it with a proper
            // error. Widening the pipe DACL is never the right answer to a parsing problem.
            Debug.WriteLine($"Unable to read '{manifestPath}' while computing the controller pipe authorization: {ex}");
            return [];
        }

        // Note this asks RunsInAppContainer, not UsesLaunchActivationArguments: a packagedClassicApp whose
        // TrustLevel is appContainer receives ordinary argv but is still sandboxed, so it needs the grant.
        bool alwaysAuthorize = string.Equals(mode, AlwaysMode, StringComparison.OrdinalIgnoreCase);
        return !alwaysAuthorize && application?.RunsInAppContainer != true
            ? []
            : AppContainerSecurityIdentifier.TryDerive(manifestInfo.PackageFamilyName) is { } securityIdentifier
                ? [securityIdentifier]
                : [];
    }
}
