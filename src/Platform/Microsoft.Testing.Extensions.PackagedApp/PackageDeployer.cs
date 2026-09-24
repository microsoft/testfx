// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;

#if PACKAGEDAPP_WINRT
using Microsoft.Testing.Extensions.PackagedApp.Interop;

using Windows.Management.Deployment;
#endif

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// Registers a packaged (MSIX) loose layout with the OS and activates it by Application User Model ID,
/// using only public, redistributable Windows APIs — the <c>PackageManager</c> WinRT class for
/// registration and <c>ApplicationActivationManager</c> for activation. This is the equivalent
/// of the Visual-Studio-internal deployment components VSTest relies on, implemented without them.
/// Callers that activate the app must hold a <see cref="PackageRegistrationLock"/> lease across
/// registration and activation.
/// </summary>
internal static class PackageDeployer
{
#if PACKAGEDAPP_WINRT
    /// <summary>
    /// Registers the loose layout described by <paramref name="manifestPath"/> in place (no copy) and
    /// verifies its registered location before any activation handoffs are written.
    /// </summary>
    /// <param name="manifestPath">The full path to the layout's <c>AppxManifest.xml</c>.</param>
    /// <param name="manifestInfo">The identity read from the manifest held open by the caller.</param>
    /// <param name="cancellationToken">A token to observe while registering.</param>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public static async Task RegisterAsync(
        string manifestPath,
        AppxManifestInfo manifestInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var packageManager = new PackageManager();
        await EnsureRecipeDependenciesAsync(packageManager, manifestPath, cancellationToken).ConfigureAwait(false);

        string packageFamilyName = manifestInfo.PackageFamilyName;
        string layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

        RegisteredPackageInfo[] existingPackages = packageManager
            .FindPackagesForUserWithPackageTypes(string.Empty, packageFamilyName, PackageTypes.Main)
            .Select(static package => new RegisteredPackageInfo(package.Id.FullName, package.InstalledPath, package.IsDevelopmentMode))
            .ToArray();
        if (IsRegisteredFromLayout(existingPackages, layoutDirectory))
        {
            return;
        }

        // DeveloperMode registers the unsigned build-output layout in place. It requires Developer Mode
        // (or sideloading) to be enabled on the machine, exactly like 'Add-AppxPackage -Register'.
        var options = new RegisterPackageOptions
        {
            DeveloperMode = true,
        };

        await RegisterAsync(
            manifestPath,
            async token =>
            {
                token.ThrowIfCancellationRequested();
                // Windows deployment requests are not safely interruptible once started. Await the
                // underlying operation so the registration lock covers every shared-state mutation.
                DeploymentResult result = await packageManager
                    .RegisterPackageByUriAsync(new Uri(manifestPath), options)
                    .AsTask()
                    .ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                // IsRegistered is authoritative for registration; ExtendedErrorCode can be informational.
                if (!result.IsRegistered)
                {
                    throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
                }
            },
            () => packageManager
                .FindPackagesForUserWithPackageTypes(string.Empty, packageFamilyName, PackageTypes.Main)
                .Select(static package => new RegisteredPackageInfo(package.Id.FullName, package.InstalledPath, package.IsDevelopmentMode))
                .ToArray(),
            async (packageFullName, token) =>
            {
                token.ThrowIfCancellationRequested();
                // Keep the lease until removal actually finishes; cancellation is handled by the
                // transaction below, which restores a usable registration before surfacing it.
                DeploymentResult result = await packageManager
                    .RemovePackageAsync(packageFullName, RemovalOptions.PreserveApplicationData)
                    .AsTask()
                    .ConfigureAwait(false);

                if (result.ExtendedErrorCode is { HResult: < 0 })
                {
                    throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Activates a registered packaged app and returns its process id.</summary>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public static uint Activate(string appUserModelId, string? activationArguments)
        => ApplicationActivationManager.ActivateApplication(appUserModelId, activationArguments);

    private static async Task EnsureRecipeDependenciesAsync(
        PackageManager packageManager,
        string manifestPath,
        CancellationToken cancellationToken)
    {
        string layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        string[] recipePaths = Directory.GetFiles(layoutDirectory, "*.build.appxrecipe", SearchOption.TopDirectoryOnly);
        if (recipePaths.Length != 1)
        {
            return;
        }

        var manifest = XDocument.Load(manifestPath);
        string? targetArchitecture = manifest.Root?
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Identity")?
            .Attribute("ProcessorArchitecture")?
            .Value;
        if (targetArchitecture is null)
        {
            return;
        }

        Windows.ApplicationModel.Package[] installedPackages = [.. packageManager.FindPackagesForUser(string.Empty)];
        var recipe = XDocument.Load(recipePaths[0]);
        foreach (XElement dependency in recipe.Descendants().Where(element => element.Name.LocalName == "ResolvedSDKReference"))
        {
            string? name = dependency.Elements().FirstOrDefault(element => element.Name.LocalName == "Name")?.Value;
            string? version = dependency.Elements().FirstOrDefault(element => element.Name.LocalName == "Version")?.Value;
            string? architecture = dependency.Elements().FirstOrDefault(element => element.Name.LocalName == "Architecture")?.Value;
            string? appxLocation = dependency.Elements().FirstOrDefault(element => element.Name.LocalName == "AppxLocation")?.Value;
            if (name is null
                || version is null
                || architecture is null
                || appxLocation is null
                || !string.Equals(architecture, targetArchitecture, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var minimumVersion = Version.Parse(version);
            if (installedPackages.Any(package =>
                string.Equals(package.Id.Name, name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(package.Id.Architecture.ToString(), architecture, StringComparison.OrdinalIgnoreCase)
                && new Version(
                    package.Id.Version.Major,
                    package.Id.Version.Minor,
                    package.Id.Version.Build,
                    package.Id.Version.Revision) >= minimumVersion))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string dependencyPath = Uri.UnescapeDataString(appxLocation);
            DeploymentResult result = await packageManager
                .AddPackageAsync(new Uri(Path.GetFullPath(dependencyPath)), dependencyPackageUris: null, DeploymentOptions.None)
                .AsTask()
                .ConfigureAwait(false);
            if (result.ExtendedErrorCode is { HResult: < 0 })
            {
                throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
            }
        }
    }
#endif

    internal static async Task RegisterAsync(
        string manifestPath,
        Func<CancellationToken, Task> registerPackage,
        Func<IReadOnlyList<RegisteredPackageInfo>> findRegisteredPackages,
        Func<string, CancellationToken, Task> removeDevelopmentPackage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

        await RegisterPackageAsync(manifestPath, registerPackage, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RegisteredPackageInfo> packages = findRegisteredPackages();
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsRegisteredFromLayout(packages, layoutDirectory))
        {
            if (packages.Count != 1)
            {
                throw CreateLocationMismatchException(layoutDirectory, packages);
            }

            RegisteredPackageInfo previousPackage = packages[0];
            if (!previousPackage.IsDevelopmentMode)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.PackagedAppRegistrationNotDevelopment,
                        previousPackage.FullName,
                        previousPackage.InstalledPath,
                        layoutDirectory));
            }

            // A successful same-version registration can retain the old layout. Only development
            // registrations support removal with PreserveApplicationData; never uninstall a retail app.
            cancellationToken.ThrowIfCancellationRequested();
            await RemovePackageAsync(manifestPath, previousPackage.FullName, removeDevelopmentPackage, cancellationToken).ConfigureAwait(false);

            // Removal can report IsRegistered=true even after the registration is gone.
            // Query separately so lookup failures are not relabeled as deployment failures.
            packages = findRegisteredPackages();
            if (packages.Count != 0)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.PackagedAppRegistrationRemovalIncomplete,
                        previousPackage.FullName,
                        previousPackage.InstalledPath,
                        layoutDirectory));
            }

            // Once the previous registration is gone, finish restoring a usable package identity
            // even if the caller cancels. Cancellation is observed after the replacement is verified.
            await RegisterPackageAsync(manifestPath, registerPackage, CancellationToken.None).ConfigureAwait(false);
            packages = findRegisteredPackages();
            if (!IsRegisteredFromLayout(packages, layoutDirectory))
            {
                throw CreateLocationMismatchException(layoutDirectory, packages);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task RegisterPackageAsync(
        string manifestPath,
        Func<CancellationToken, Task> registerPackage,
        CancellationToken cancellationToken)
    {
        try
        {
            await registerPackage(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CreateDeploymentFailureException(manifestPath, ex);
        }
    }

    private static async Task RemovePackageAsync(
        string manifestPath,
        string packageFullName,
        Func<string, CancellationToken, Task> removeDevelopmentPackage,
        CancellationToken cancellationToken)
    {
        try
        {
            await removeDevelopmentPackage(packageFullName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.PackagedAppRegistrationRemovalFailed,
                    packageFullName,
                    manifestPath,
                    ex.Message),
                ex);
        }
    }

    private static InvalidOperationException CreateDeploymentFailureException(string manifestPath, Exception exception)
        => new(
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.PackagedAppRegistrationFailed, manifestPath, exception.Message),
            exception);

    private static bool IsRegisteredFromLayout(IReadOnlyList<RegisteredPackageInfo> packages, string layoutDirectory)
        => packages.Count == 1
        && string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(packages[0].InstalledPath)),
            layoutDirectory,
            StringComparison.OrdinalIgnoreCase);

    private static InvalidOperationException CreateLocationMismatchException(string layoutDirectory, IReadOnlyList<RegisteredPackageInfo> packages)
        => new(string.Format(
            CultureInfo.CurrentCulture,
            ExtensionResources.PackagedAppRegistrationLocationMismatch,
            layoutDirectory,
            string.Join(", ", packages.Select(static package => package.InstalledPath))));
}
