// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class AppxManifestInfo
{
    /// <summary>Reads and parses the manifest at <paramref name="manifestPath"/>.</summary>
    /// <param name="manifestPath">The path to an <c>AppxManifest.xml</c>.</param>
    /// <returns>The parsed manifest info.</returns>
    public static AppxManifestInfo ReadFromManifest(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return ReadFromManifest(stream);
    }

    /// <summary>Reads and parses a manifest from <paramref name="manifestStream"/>.</summary>
    /// <param name="manifestStream">A stream over an <c>AppxManifest.xml</c>.</param>
    /// <returns>The parsed manifest info.</returns>
    public static AppxManifestInfo ReadFromManifest(Stream manifestStream)
    {
        var document = XDocument.Load(manifestStream);
        XElement? root = document.Root;

        // Match by local name so we are resilient to the manifest schema version (the foundation
        // namespace URI changes across Windows 10 SDK revisions).
        XElement? identity = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Identity");
        string? name = identity?.Attribute("Name")?.Value;
        string? publisher = identity?.Attribute("Publisher")?.Value;

        if (name is null || name.Length == 0 || publisher is null || publisher.Length == 0)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidAppxManifestMissingIdentity);
        }

        string packageFamilyName = $"{name}_{ComputePublisherId(publisher)}";

        bool canRunFullTrust = root is not null
            && root.Descendants()
            .Any(static element =>
                element.Name.LocalName == "Capability"
                && string.Equals(element.Attribute("Name")?.Value, "runFullTrust", StringComparison.OrdinalIgnoreCase));

        List<XElement> applicationElements = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Applications")?
            .Elements().Where(static e => e.Name.LocalName == "Application")
            .ToList()
            ?? [];

        // A package may declare several applications, each with its own id (and AUMID). Capture them
        // all so the launcher can resolve the one matching the executable it was asked to launch,
        // instead of guessing with the first entry.
        var applications = applicationElements
            .Select(application =>
            {
                string? applicationId = application.Attribute("Id")?.Value;
                string? executable = application.Attribute("Executable")?.Value;
                string? entryPoint = application.Attribute("EntryPoint")?.Value;
                string? trustLevel = application.Attributes().FirstOrDefault(static attribute => attribute.Name.LocalName == "TrustLevel")?.Value;
                string? runtimeBehavior = application.Attributes().FirstOrDefault(static attribute => attribute.Name.LocalName == "RuntimeBehavior")?.Value;
                bool hasFullTrustCompanion = application.Descendants()
                    .Any(static element =>
                        element.Name.LocalName == "Extension"
                        && string.Equals(element.Attribute("Category")?.Value, "windows.fullTrustProcess", StringComparison.OrdinalIgnoreCase));
                return applicationId is null || applicationId.Length == 0
                    ? null
                    : new AppxApplicationInfo(
                        applicationId,
                        executable,
                        $"{packageFamilyName}!{applicationId}",
                        UsesLaunchActivationArguments(
                            entryPoint,
                            trustLevel,
                            runtimeBehavior,
                            canUsePackageFullTrustFallback:
                                canRunFullTrust
                                && applicationElements.Count == 1
                                && !hasFullTrustCompanion),
                        RunsInAppContainer(
                            entryPoint,
                            trustLevel,
                            runtimeBehavior,
                            canUsePackageFullTrustFallback:
                                canRunFullTrust
                                && applicationElements.Count == 1
                                && !hasFullTrustCompanion));
            })
            .OfType<AppxApplicationInfo>()
            .ToList();

        return new AppxManifestInfo(name, publisher, applications);
    }

    /// <summary>
    /// Classifies whether an application runs inside an AppContainer, i.e. with a restricted token. This is
    /// the question the controller-connection authorization asks, and it differs from
    /// <see cref="UsesLaunchActivationArguments"/> in one shape: a <c>packagedClassicApp</c> whose
    /// <c>TrustLevel</c> is <c>appContainer</c> is sandboxed but still receives process <c>argv</c>.
    /// </summary>
    private static bool RunsInAppContainer(
        string? entryPoint,
        string? trustLevel,
        string? runtimeBehavior,
        bool canUsePackageFullTrustFallback)
        // An explicit trust level wins over the activation model: it is a direct statement about the token
        // the host will run with. In particular, a windowsApp can use launch activation while explicitly
        // running at medium integrity, so UsesLaunchActivationArguments cannot answer this question alone.
        => trustLevel switch
        {
            string value when string.Equals(value, "appContainer", StringComparison.OrdinalIgnoreCase) => true,
            string value when string.Equals(value, "mediumIL", StringComparison.OrdinalIgnoreCase) => false,
            _ => UsesLaunchActivationArguments(entryPoint, trustLevel, runtimeBehavior, canUsePackageFullTrustFallback),
        };

    private static bool UsesLaunchActivationArguments(
        string? entryPoint,
        string? trustLevel,
        string? runtimeBehavior,
        bool canUsePackageFullTrustFallback)
    {
        // RuntimeBehavior determines the activation model. A packaged classic or Win32 application
        // receives process argv even when its sandbox TrustLevel is appContainer.
        if (string.Equals(runtimeBehavior, "packagedClassicApp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtimeBehavior, "win32App", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryPoint, "Windows.FullTrustApplication", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(runtimeBehavior, "windowsApp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(trustLevel, "appContainer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(trustLevel, "mediumIL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Legacy UWP manifests express AppContainer by omission. A package-wide runFullTrust capability
        // is a safe fallback only for one application with no full-trust companion extension (the shape
        // produced by packaged WinUI). Mixed packages and UWP apps with a windows.fullTrustProcess helper
        // need application-specific desktop evidence.
        return !canUsePackageFullTrustFallback;
    }
}
