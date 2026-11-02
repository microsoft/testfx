// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// The PackagedApp extension only targets .NET (net8.0/net9.0), so these tests are compiled only there.
#if !NETFRAMEWORK

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Win32;

namespace Microsoft.Testing.Extensions.UnitTests;

/// <summary>
/// Covers the derivation of a packaged app's AppContainer SID, which is the exact identity the launcher asks
/// the platform to authorize on the test host controller pipe. A wrong SID here either breaks the
/// AppContainer connect-back or puts an identity in the pipe DACL that was not intended, so it is
/// cross-checked against the mappings Windows itself registered on the machine.
/// </summary>
[TestClass]
public sealed class AppContainerSecurityIdentifierTests
{
    private const string ContosoPackageFamilyName = "Contoso.MyTestApp_8wekyb3d8bbwe";
    private const string ContosoPackageSid = "S-1-15-2-1990679259-4123976751-842158434-3026549936-2944832882-252165955-409282942";

    [TestMethod]
    public void TryDerive_ProducesTheAppContainerSidOfThePackage()
        => Assert.AreEqual(ContosoPackageSid, AppContainerSecurityIdentifier.TryDerive(ContosoPackageFamilyName));

    /// <summary>
    /// A ground-truth vector captured from a real Windows <c>AppContainer\Mappings</c> registration, so the
    /// derivation is pinned against a SID the operating system actually assigned rather than only against a
    /// value this same algorithm produced.
    /// </summary>
    /// <remarks>
    /// <see cref="TryDerive_MatchesTheAppContainerMappingsRegisteredByWindows"/> sweeps every mapping present
    /// on the current machine, which is stronger — but it self-skips on a machine that has none (a Server
    /// Core CI image, for example). This case keeps one real cross-check running everywhere, including on
    /// non-Windows, since the derivation is pure managed code.
    /// </remarks>
    [TestMethod]
    public void TryDerive_MatchesASidWindowsActuallyAssigned()
        => Assert.AreEqual(
            "S-1-15-2-1050576210-4101474698-56307613-2706264498-167457550-835605972-784472318",
            AppContainerSecurityIdentifier.TryDerive("Microsoft.WindowsNotepad_8wekyb3d8bbwe"));

    // A package family name is case-insensitive and Windows lower-cases the moniker before hashing it, so
    // any casing must map to the same container.
    [TestMethod]
    public void TryDerive_IsCaseInsensitiveAndTrimsWhitespace()
    {
        Assert.AreEqual(ContosoPackageSid, AppContainerSecurityIdentifier.TryDerive(ContosoPackageFamilyName.ToUpperInvariant()));
        Assert.AreEqual(ContosoPackageSid, AppContainerSecurityIdentifier.TryDerive(ContosoPackageFamilyName.ToLowerInvariant()));
        Assert.AreEqual(ContosoPackageSid, AppContainerSecurityIdentifier.TryDerive($"  {ContosoPackageFamilyName}  "));
    }

    [TestMethod]
    public void TryDerive_ProducesADistinctSidPerPackage()
        => Assert.AreNotEqual(
            AppContainerSecurityIdentifier.TryDerive(ContosoPackageFamilyName),
            AppContainerSecurityIdentifier.TryDerive("Fabrikam.OtherApp_8wekyb3d8bbwe"));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void TryDerive_WithoutAPackageFamilyName_ReturnsNull(string? packageFamilyName)
        => Assert.IsNull(AppContainerSecurityIdentifier.TryDerive(packageFamilyName));

    /// <summary>
    /// Ground truth: Windows records the SID it assigned to every AppContainer it knows about under
    /// <c>HKCU\…\AppContainer\Mappings</c>, together with the moniker (the package family name, lower-cased).
    /// Deriving each of those monikers must reproduce the very SID Windows registered, which is what proves
    /// the derivation matches the identity a real AppContainer test host actually runs under.
    /// </summary>
    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows, IgnoreMessage = "AppContainer mappings are a Windows concept.")]
    [SupportedOSPlatform("windows")]
    public void TryDerive_MatchesTheAppContainerMappingsRegisteredByWindows()
    {
        using RegistryKey? mappings = Registry.CurrentUser.OpenSubKey(
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Mappings");

        if (mappings is null)
        {
            Assert.Inconclusive("This machine has no AppContainer mappings to cross-check against.");
            return;
        }

        int verified = 0;
        foreach (string registeredSid in mappings.GetSubKeyNames())
        {
            using RegistryKey? mapping = mappings.OpenSubKey(registeredSid);
            if (mapping?.GetValue("Moniker") is not string moniker || moniker.Length == 0)
            {
                continue;
            }

            // Child AppContainers (a package SID plus extra sub-authorities) are registered here too, and
            // are not derived from a package family name.
            if (registeredSid.Split('-').Length != 11)
            {
                continue;
            }

            Assert.AreEqual(registeredSid, AppContainerSecurityIdentifier.TryDerive(moniker), $"Mismatch for moniker '{moniker}'.");
            verified++;
        }

        if (verified == 0)
        {
            Assert.Inconclusive("This machine has no package AppContainer mapping to cross-check against.");
        }
    }
}

#endif
