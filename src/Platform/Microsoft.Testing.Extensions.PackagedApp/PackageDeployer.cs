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
    /// <param name="cancellationToken">A token to observe while registering.</param>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public static Task RegisterAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var packageManager = new PackageManager();
        string packageFamilyName = AppxManifestInfo.ReadFromManifest(manifestPath).PackageFamilyName;

        // DeveloperMode registers the unsigned build-output layout in place. It requires Developer Mode
        // (or sideloading) to be enabled on the machine, exactly like 'Add-AppxPackage -Register'.
        var options = new RegisterPackageOptions
        {
            DeveloperMode = true,
        };

        return RegisterAsync(
            manifestPath,
            async token =>
            {
                DeploymentResult result = await packageManager
                    .RegisterPackageByUriAsync(new Uri(manifestPath), options)
                    .AsTask(token)
                    .ConfigureAwait(false);

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
                DeploymentResult result = await packageManager
                    .RemovePackageAsync(packageFullName, RemovalOptions.PreserveApplicationData)
                    .AsTask(token)
                    .ConfigureAwait(false);

                if (result.ExtendedErrorCode is { HResult: < 0 })
                {
                    throw new InvalidOperationException(result.ErrorText, result.ExtendedErrorCode);
                }
            },
            cancellationToken);
    }

    /// <summary>Activates a registered packaged app and returns its process id.</summary>
    [SupportedOSPlatform("windows10.0.19041.0")]
    public static uint Activate(string appUserModelId, string? activationArguments)
        => ApplicationActivationManager.ActivateApplication(appUserModelId, activationArguments);
#endif

    internal static async Task RegisterAsync(
        string manifestPath,
        Func<CancellationToken, Task> registerPackage,
        Func<IReadOnlyList<RegisteredPackageInfo>> findRegisteredPackages,
        Func<string, CancellationToken, Task> removeDevelopmentPackage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string layoutDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;

            await registerPackage(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<RegisteredPackageInfo> packages = findRegisteredPackages();

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
                await removeDevelopmentPackage(previousPackage.FullName, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await registerPackage(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                packages = findRegisteredPackages();
                if (!IsRegisteredFromLayout(packages, layoutDirectory))
                {
                    throw CreateLocationMismatchException(layoutDirectory, packages);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected; let it propagate unwrapped.
            throw;
        }
        catch (Exception ex)
        {
            // Include the requested manifest even when a Windows operation faults instead of returning
            // a DeploymentResult, or Windows reports success without registering the requested location.
            throw new InvalidOperationException(
                string.Format(CultureInfo.CurrentCulture, ExtensionResources.PackagedAppRegistrationFailed, manifestPath, ex.Message),
                ex);
        }
    }

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
