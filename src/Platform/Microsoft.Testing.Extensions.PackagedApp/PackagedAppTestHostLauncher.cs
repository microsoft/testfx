// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// An <see cref="ITestHostLauncher"/> for Windows test applications. It handles two layouts:
/// a packaged, full-trust MSIX desktop host, which cannot be started with <c>Process.Start</c> and is
/// instead registered with the OS and activated by Application User Model ID (AUMID); and — when
/// explicitly opted in — a non-packaged (loose-layout) host, which is deployed into an isolated
/// directory and launched from there.
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
/// writable data folder (see <see cref="PackagedAppConnectBackHandshake"/>). A packaged full-trust
/// desktop app receives the platform-prepared command line as <c>argv</c>; an AppContainer app receives
/// a versioned activation payload that its <c>OnLaunched</c> bootstrap restores to the same logical
/// argument array.
/// </para>
/// <para>
/// Both process-argv and windowsApp/UWP launch-activation argument delivery are supported, and this
/// launcher additionally authorizes the package's own AppContainer SID on the controller connection
/// through <see cref="ITestHostControllerConnectionAuthorizer"/> (see
/// <see cref="GetAuthorizedSecurityIdentitiesAsync"/>), which is what lets a restricted
/// AppContainer token reach the controller at all. The full register-and-activate path
/// ships only in the Windows build of this extension (<c>net*-windows10.0.19041.0</c>), where the
/// <c>PackageManager</c> WinRT projection is available. The plain <c>net8.0</c>/<c>net9.0</c> build
/// still deploys and launches an opted-in non-packaged loose layout, but rejects a packaged layout with
/// an actionable error so a consumer that resolves that build is told to target a Windows TFM. Registering
/// an unsigned build-output layout additionally requires Developer Mode (or sideloading) to be enabled
/// on the machine.
/// </para>
/// <para>
/// In all cases the platform owns argument/environment preparation, the controller-to-host IPC pipe,
/// the PID handshake, and the lifetime-handler dispatch; this launcher only performs the
/// deploy/register-and-create step and returns an <see cref="ITestHostHandle"/> the platform monitors.
/// </para>
/// <para>
/// Registering an <em>enabled</em> launcher forces the platform onto the test host controller
/// (process restart) model, because a custom launcher only has an effect when an out-of-process test
/// host is started. That is the right trade for a packaged app, which genuinely cannot be started in
/// place, but it is pure overhead for an app that is simply started with <c>Process.Start</c> — most
/// notably an <em>unpackaged</em> WinUI test app, which additionally does not want its layout copied
/// to a deployment directory. The launcher therefore reports itself enabled only when it actually has
/// work to do; see <see cref="IsEnabledAsync"/>.
/// </para>
/// </remarks>
internal sealed class PackagedAppTestHostLauncher : ITestHostLauncher, ITestHostControllerConnectionAuthorizer
{
    /// <summary>
    /// The environment variable that overrides how the launcher decides whether to take over the test
    /// host launch. Accepted values are <c>auto</c> (the default when unset), <c>always</c> and
    /// <c>never</c>, compared case-insensitively; any other value is treated as <c>auto</c> rather than
    /// failing a run over a typo in an environment variable.
    /// </summary>
    internal const string LauncherModeEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER";

    /// <summary>Always take over the launch, even for a non-packaged (loose) layout.</summary>
    private const string AlwaysMode = "always";

    /// <summary>Never take over the launch, even for a packaged layout.</summary>
    private const string NeverMode = "never";

    /// <summary>
    /// The environment variable that overrides whether the launcher asks the platform to authorize this
    /// package's AppContainer SID on the test host controller pipe. Accepted values are <c>auto</c> (the
    /// default when unset, which authorizes only when the manifest says the app runs in an AppContainer),
    /// <c>always</c> and <c>never</c>, compared case-insensitively; any other value is treated as
    /// <c>auto</c> rather than failing a run over a typo in an environment variable.
    /// </summary>
    internal const string PipeAuthorizationModeEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_PIPEAUTHORIZATION";

    // The handoff is an explicit allowlist of protocol metadata: controller connect-back values,
    // TRX/HangDump pipe endpoints, and Retry attempt/run correlation. Some correlation values may be
    // supplied by CI or the user, but arbitrary environment values remain excluded because broader
    // TESTINGPLATFORM_* values such as inline runsettings can carry secrets.
    private const string ConnectBackEnvironmentVariablePrefix = "TESTINGPLATFORM_TESTHOSTCONTROLLER_";
    private const string HangDumpPipeEnvironmentVariableName = "TESTINGPLATFORM_HANGDUMP_PIPENAME";
    private const string LogicalRunIdEnvironmentVariableName = "TESTINGPLATFORM_LOGICAL_RUN_ID";
    // These names must match the reporter packages' JournalEnvironmentVariableName constants. They are repeated
    // here intentionally so PackagedApp does not take dependencies on every report package just to forward launch metadata.
    private const string CtrfReportJournalEnvironmentVariableName = "TESTINGPLATFORM_CTRFREPORT_JOURNAL";
    private const string HtmlReportJournalEnvironmentVariableName = "TESTINGPLATFORM_HTMLREPORT_JOURNAL";
    private const string JUnitReportJournalEnvironmentVariableName = "TESTINGPLATFORM_JUNITREPORT_JOURNAL";
    private const string RetryAttemptEnvironmentVariableName = "TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER";
    private const string RetryRecoveredArtifactManifestEnvironmentVariableName = "TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST";
    private const string TrxTestRunIdEnvironmentVariableName = "TESTINGPLATFORM_TRX_TESTRUN_ID";
    private const string TrxPipeEnvironmentVariableName = "TRXNAMEDPIPENAME";

    private readonly string _testApplicationDirectory;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public PackagedAppTestHostLauncher()
        // The controller process runs the very test application whose host is about to be launched, so
        // its base directory is the layout the platform will ask this launcher to start. The launch
        // context (which carries the authoritative path) does not exist yet when enablement is decided.
        : this(AppContext.BaseDirectory, Environment.GetEnvironmentVariable)
    {
    }

    internal PackagedAppTestHostLauncher(string testApplicationDirectory, Func<string, string?> getEnvironmentVariable)
    {
        _testApplicationDirectory = testApplicationDirectory;
        _getEnvironmentVariable = getEnvironmentVariable;
    }

    public string Uid => nameof(PackagedAppTestHostLauncher);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.PackagedAppExtensionDisplayName;

    public string Description => ExtensionResources.PackagedAppExtensionDescription;

    /// <summary>
    /// Reports whether this launcher should take over starting the test host.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enabling a launcher is not free: the platform switches to the test host controller (process
    /// restart) model for the whole run. The launcher therefore opts in only when the layout really
    /// needs it — that is, when it is a packaged (MSIX) layout, which cannot be started with
    /// <c>Process.Start</c> at all. A non-packaged layout (an ordinary console test app, or an
    /// unpackaged WinUI app) is left to the platform's default in-process/<c>Process.Start</c> path, so
    /// referencing this package in such an app costs nothing and never copies its layout to a
    /// deployment directory.
    /// </para>
    /// <para>
    /// Packaged Windows apps (UWP/WinUI) are a Windows-only concept, so the launcher stays disabled on
    /// every other operating system regardless of the layout or the override.
    /// </para>
    /// <para>
    /// <see cref="LauncherModeEnvironmentVariable"/> overrides the layout probe: <c>always</c> opts an
    /// explicitly non-packaged (loose) layout into deploy-and-launch, and <c>never</c> is the escape
    /// hatch that keeps the launcher out of the way even for a packaged layout.
    /// </para>
    /// </remarks>
    /// <returns>A task producing <see langword="true"/> when the launcher should be registered.</returns>
    public Task<bool> IsEnabledAsync() => Task.FromResult(IsEnabled());

    private bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        string? mode = _getEnvironmentVariable(LauncherModeEnvironmentVariable)?.Trim();

        // 'never' wins over everything (escape hatch), 'always' opts a loose layout in, and anything
        // else — including an unset or misspelled value — falls back to probing the layout.
        return !string.Equals(mode, NeverMode, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(mode, AlwaysMode, StringComparison.OrdinalIgnoreCase)
                || AppxManifestInfo.FindManifestPath(_testApplicationDirectory) is not null);
    }

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
        return Task.FromResult(GetAuthorizedAppContainerSecurityIdentifiers(testHostFileName));
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

    public async Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Honor immediate cancellation before doing any (potentially expensive) deployment work.
        cancellationToken.ThrowIfCancellationRequested();

        string sourceDirectory = Path.GetDirectoryName(context.FileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{context.FileName}'.");

        // A packaged (MSIX) app is detected by a matching AppxManifest.xml. The manifest lives at the
        // package layout root, which may be an ancestor of the executable's directory
        // (Application/@Executable can point into a subdirectory), so search upward rather than only the
        // executable's directory while rejecting stray ancestor manifests that do not describe this host.
        string? manifestPath = AppxManifestInfo.FindManifestPath(sourceDirectory, context.FileName);
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
        await PackageDeployer.RegisterAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        // Hand off the explicit safe environment allowlist through package LocalState. Controller-host runs
        // key the file by controller PID; retry runs key it by a hash of their unique pipe name. An
        // AUMID-activated process does not inherit the controller's environment, so the activated host
        // applies these values before platform and extension connect-back initialization.
        string? handshakeId = PackagedAppConnectBackHandshake.TryGetHandshakeId(context.Arguments);
        string? handshakePath = null;
        string? activationPayloadPath = null;
        try
        {
            if (handshakeId is not null)
            {
                handshakePath = PackagedAppConnectBackHandshake.GetHandshakeFilePath(manifestInfo.PackageFamilyName, handshakeId);
                PackagedAppConnectBackHandshake.Write(
                    handshakePath,
                    GetConnectBackEnvironment(context).Append(new(LauncherModeEnvironmentVariable, NeverMode)));
            }

            PackagedAppActivationData activationData = CreateActivationArguments(
                application,
                context.Arguments,
                PackagedAppConnectBackHandshake.GetHandshakeDirectory(manifestInfo.PackageFamilyName));
            string activationArguments = activationData.Arguments;
            activationPayloadPath = activationData.PayloadPath;

            cancellationToken.ThrowIfCancellationRequested();
            uint processId = PackageDeployer.Activate(application.AppUserModelId, activationArguments);

            // The handle owns deleting the hand-off from now on: the activated host normally consumes and
            // deletes it, but if that host exits before reading it the handle still removes it on dispose,
            // so connect-back data is never left behind.
            return new ActivatedAppTestHostHandle(processId, handshakePath, activationPayloadPath);
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
            throw;
        }
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
        AppxApplicationInfo? application = manifestInfo.ResolveApplication(Path.GetDirectoryName(manifestPath)!, context.FileName);
        throw new InvalidOperationException(
            string.Format(
                CultureInfo.CurrentCulture,
                ExtensionResources.PackagedAppLaunchNotSupported,
                application?.AppUserModelId ?? manifestInfo.PackageFamilyName,
                manifestPath));
    }
#endif

    // Selects the controller connect-back values, TRX/HangDump endpoints, and Retry attempt/run
    // correlation metadata that an AUMID-activated host would not otherwise inherit. Unrelated
    // environment values remain excluded because they can contain user data or secrets.
    internal static IEnumerable<KeyValuePair<string, string?>> GetConnectBackEnvironment(TestHostLaunchContext context)
    {
        foreach (KeyValuePair<string, string?> environmentVariable in context.EnvironmentVariables)
        {
            if (environmentVariable.Key.StartsWith(ConnectBackEnvironmentVariablePrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, CtrfReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, HtmlReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, JUnitReportJournalEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, RetryAttemptEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, RetryRecoveredArtifactManifestEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, LogicalRunIdEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, TrxTestRunIdEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, TrxPipeEnvironmentVariableName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(environmentVariable.Key, HangDumpPipeEnvironmentVariableName, StringComparison.OrdinalIgnoreCase))
            {
                yield return environmentVariable;
            }
        }
    }

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
