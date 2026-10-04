// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;

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
internal sealed partial class PackagedAppTestHostLauncher : ITestHostLauncher, ITestHostControllerConnectionAuthorizer
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

    /// <summary>
    /// The full path of the packaged test-host executable selected by an external full-trust controller.
    /// </summary>
    private const string TargetExecutableEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_TARGET";

    // The handoff is an explicit allowlist of protocol metadata: controller connect-back values,
    // dotnet-test execution identity, TRX/HangDump pipe endpoints, and Retry attempt/run correlation.
    // Some correlation values may be supplied by CI or the user, but arbitrary environment values
    // remain excluded because broader TESTINGPLATFORM_* values such as inline runsettings can carry secrets.
    private const string ConnectBackEnvironmentVariablePrefix = "TESTINGPLATFORM_TESTHOSTCONTROLLER_";
    private const string DotnetTestExecutionIdEnvironmentVariableName = "TESTINGPLATFORM_DOTNETTEST_EXECUTIONID";
    private const string HangDumpPipeEnvironmentVariableName = "TESTINGPLATFORM_HANGDUMP_PIPENAME";
    private const string LogicalRunIdEnvironmentVariableName = "TESTINGPLATFORM_LOGICAL_RUN_ID";
    private const string MSBuildNodeOption = "--internal-msbuild-node";
    private const string TestHostControllerSkipExtensionEnvironmentVariableName = "TESTINGPLATFORM_TESTHOSTCONTROLLER_SKIPEXTENSION";
    // These names must match the reporter packages' JournalEnvironmentVariableName constants. They are repeated
    // here intentionally so PackagedApp does not take dependencies on every report package just to forward launch metadata.
    private const string CtrfReportJournalEnvironmentVariableName = "TESTINGPLATFORM_CTRFREPORT_JOURNAL";
    private const string HtmlReportJournalEnvironmentVariableName = "TESTINGPLATFORM_HTMLREPORT_JOURNAL";
    private const string JUnitReportJournalEnvironmentVariableName = "TESTINGPLATFORM_JUNITREPORT_JOURNAL";
    private const string RetryAttemptEnvironmentVariableName = "TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER";
    private const string RetryRecoveredArtifactManifestEnvironmentVariableName = "TESTINGPLATFORM_RETRY_RECOVERED_ARTIFACT_MANIFEST";
    private const string AppModelControllerExtensionsEnvironmentVariableName = "MSTEST_APPMODEL_CONTROLLER_EXTENSIONS";
    private const string TrxTestRunIdEnvironmentVariableName = "TESTINGPLATFORM_TRX_TESTRUN_ID";
    private const string TrxPipeEnvironmentVariableName = "TRXNAMEDPIPENAME";
#if PACKAGEDAPP_WINRT
    private const string AppContainerArtifactRootsConfiguredEnvironmentVariableName = "TESTINGPLATFORM_PACKAGEDAPP_APPCONTAINER_ARTIFACT_ROOTS_CONFIGURED";
    private const string ArtifactPathSourceRootEnvironmentVariableName = "TESTINGPLATFORM_ARTIFACT_PATH_SOURCE_ROOT";
    private const string ArtifactPathDestinationRootEnvironmentVariableName = "TESTINGPLATFORM_ARTIFACT_PATH_DESTINATION_ROOT";
    private const string DiagnosticArtifactPathSourceRootEnvironmentVariableName = "TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_SOURCE_ROOT";
    private const string DiagnosticArtifactPathDestinationRootEnvironmentVariableName = "TESTINGPLATFORM_DIAGNOSTIC_ARTIFACT_PATH_DESTINATION_ROOT";
#endif
    private const string ResultsDirectoryOption = "--results-directory";
    private const string DiagnosticOutputDirectoryOption = "--diagnostic-output-directory";

    private readonly string _testApplicationDirectory;
    private readonly bool _isActivatedChild;
    private readonly string? _targetExecutable;
    private readonly Func<string, string?> _getEnvironmentVariable;

    public PackagedAppTestHostLauncher()
        // The controller process runs the very test application whose host is about to be launched, so
        // its base directory is the layout the platform will ask this launcher to start. The launch
        // context (which carries the authoritative path) does not exist yet when enablement is decided.
        : this(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable,
            IsActivatedChild(Environment.GetCommandLineArgs()))
    {
    }

    internal PackagedAppTestHostLauncher(string testApplicationDirectory, Func<string, string?> getEnvironmentVariable)
        : this(testApplicationDirectory, getEnvironmentVariable, isActivatedChild: false)
    {
    }

    private PackagedAppTestHostLauncher(
        string testApplicationDirectory,
        Func<string, string?> getEnvironmentVariable,
        bool isActivatedChild)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
        _isActivatedChild = isActivatedChild;
        _targetExecutable = GetTargetExecutable(getEnvironmentVariable);
        _testApplicationDirectory = _targetExecutable is null
            ? testApplicationDirectory
            : Path.GetDirectoryName(_targetExecutable)!;
    }

    public string Uid => nameof(PackagedAppTestHostLauncher);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.PackagedAppExtensionDisplayName;

    public string Description => ExtensionResources.PackagedAppExtensionDescription;

    private static bool IsActivatedChild(IReadOnlyList<string> processArguments)
        => PackagedAppConnectBackHandshake.TryGetHandshakeId(processArguments) is not null;

    private string? FindManifestPath()
        // Stryker disable once Conditional: both overloads delegate to the same nullable-aware core when the target is absent.
        => _targetExecutable is null
            ? AppxManifestInfo.FindManifestPath(_testApplicationDirectory)
            : AppxManifestInfo.FindManifestPath(_testApplicationDirectory, _targetExecutable);

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
        if (!OperatingSystem.IsWindows() || _isActivatedChild)
        {
            return false;
        }

        string? mode = _getEnvironmentVariable(LauncherModeEnvironmentVariable)?.Trim();

        // 'never' wins over everything (escape hatch), 'always' opts a loose layout in, and anything
        // else — including an unset or misspelled value — falls back to probing the layout.
        return !string.Equals(mode, NeverMode, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(mode, AlwaysMode, StringComparison.OrdinalIgnoreCase)
                || FindManifestPath() is not null);
    }

    public async Task<ITestHostHandle> LaunchTestHostAsync(TestHostLaunchContext context, CancellationToken cancellationToken)
    {
        // Honor immediate cancellation before doing any (potentially expensive) deployment work.
        cancellationToken.ThrowIfCancellationRequested();

        string targetFileName = _targetExecutable ?? context.FileName;
#if PACKAGEDAPP_WINRT
        targetFileName = ResolveFinalPath(targetFileName);
        targetFileName = MaterializeAppxRecipeLayout(targetFileName, out string? appxRecipePath);
#else
        string? appxRecipePath = null;
#endif
        string sourceDirectory = Path.GetDirectoryName(targetFileName)
            ?? throw new InvalidOperationException($"Unable to determine the source directory of '{targetFileName}'.");

        if (_targetExecutable is not null)
        {
            context = new TestHostLaunchContext(
                targetFileName,
                context.Arguments,
                context.EnvironmentVariables,
                context.WorkingDirectory);
        }

        // A packaged (MSIX) app is detected by a matching AppxManifest.xml. The manifest lives at the
        // package layout root, which may be an ancestor of the executable's directory
        // (Application/@Executable can point into a subdirectory), so search upward rather than only the
        // executable's directory while rejecting stray ancestor manifests that do not describe this host.
        string? manifestPath = AppxManifestInfo.FindManifestPath(sourceDirectory, context.FileName);
        if (manifestPath is not null)
        {
            return await LaunchPackagedAsync(context, manifestPath, appxRecipePath, cancellationToken).ConfigureAwait(false);
        }

        // The layout is not packaged (no AppxManifest.xml). Deploy the loose layout into an isolated
        // directory and launch the produced executable from there.
        return LaunchLooseLayout(context, sourceDirectory, cancellationToken);
    }
}
