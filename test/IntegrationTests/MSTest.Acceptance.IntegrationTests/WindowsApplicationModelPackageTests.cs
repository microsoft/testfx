// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Verifies the physical Windows application-model assets in the configuration-matched MSTest packages.
/// </summary>
[TestClass]
public sealed class WindowsApplicationModelPackageTests
{
    [TestMethod]
    [DataRow("net10.0-windows10.0.26100.0")]
    [DataRow("net11.0-windows10.0.26100.0")]
    public void ModernUwp_ResolvedAssetsRequireMatchingFrameworkAndSpecializedPayloads(string targetFramework)
    {
        using var directory = new TempDirectory();
        string reportPath = Path.Combine(directory.Path, "resolved-mstest-assets.txt");
        string frameworkLib = targetFramework.Split('-')[0];
        string report = $$"""
            TargetFramework={{targetFramework}}
            UseUwpTools=true
            /mstest.testframework/5.0.0/lib/{{frameworkLib}}/MSTest.TestFramework.dll
            /mstest.testframework/5.0.0/lib/{{frameworkLib}}/MSTest.TestFramework.Extensions.dll
            /mstest.testadapter/5.0.0/buildTransitive/net10.0/ui/MSTest.TestAdapter.dll
            /mstest.testadapter/5.0.0/buildTransitive/net10.0/ui/MSTestAdapter.PlatformServices.dll
            /mstest.testframework/5.0.0/buildTransitive/net10.0/ui/uwp/MSTest.Uwp.dll
            """;
        File.WriteAllText(reportPath, report);

        WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
            reportPath, WindowsApplicationModelAssetKind.ModernUwp, targetFramework);

        File.WriteAllText(
            reportPath,
            report.Replace($"/lib/{frameworkLib}/MSTest.TestFramework.dll", "/lib/netstandard2.0/MSTest.TestFramework.dll", StringComparison.Ordinal));
        AssertFailedException wrongFramework = Assert.ThrowsExactly<AssertFailedException>(() =>
            WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
                reportPath, WindowsApplicationModelAssetKind.ModernUwp, targetFramework));
        Assert.Contains($"/lib/{Regex.Escape(frameworkLib)}/MSTest\\.TestFramework\\.dll", wrongFramework.Message);

        File.WriteAllText(reportPath, report.Replace("/buildTransitive/net10.0/ui/MSTest", "/lib/net10.0/MSTest", StringComparison.Ordinal));
        AssertFailedException ordinaryPayload = Assert.ThrowsExactly<AssertFailedException>(() =>
            WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
                reportPath, WindowsApplicationModelAssetKind.ModernUwp, targetFramework));
        Assert.Contains("/buildTransitive/net10\\.0/ui/MSTest\\.TestAdapter\\.dll", ordinaryPayload.Message);

        File.WriteAllText(reportPath, report + Environment.NewLine + "/mstest.testframework/5.0.0/buildTransitive/net10.0/ui/winui/MSTest.WinUI.dll");
        AssertFailedException wrongApplicationModel = Assert.ThrowsExactly<AssertFailedException>(() =>
            WindowsApplicationModelTestTools.AssertResolvedMSTestAssets(
                reportPath, WindowsApplicationModelAssetKind.ModernUwp, targetFramework));
        Assert.Contains("/buildTransitive/net10\\.0/ui/winui/", wrongApplicationModel.Message);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativeSdkEnvironment_AlignsSdkPathsAndPreservesSharedTooling(bool inheritedSdk)
    {
        string dotnetRoot = Path.Combine(Path.GetTempPath(), "native-dotnet");
        string sdkDirectory = Path.Combine(dotnetRoot, "sdk", "10.0.401");
        var expected = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NUGET_PACKAGES"] = "acceptance-package-cache",
            ["MSBUILDLOGIMPORTS"] = "1",
            ["MSBuildSDKsPath"] = Path.Combine(sdkDirectory, "Sdks"),
            ["MSBuildExtensionsPath"] = sdkDirectory,
            ["DOTNET_ROOT"] = dotnetRoot,
            ["DOTNET_ROOT_X64"] = dotnetRoot,
            ["DOTNET_INSTALL_DIR"] = dotnetRoot,
            ["DOTNET_MULTILEVEL_LOOKUP"] = "0",
            ["MSBUILDUSESERVER"] = "0",
        };
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["NUGET_PACKAGES"] = "acceptance-package-cache",
            ["MSBUILDLOGIMPORTS"] = "1",
        };
        if (inheritedSdk)
        {
            foreach (string name in expected.Keys.Except(environment.Keys))
            {
                environment[name] = "outer-sdk-value";
            }
        }

        AcceptanceTestBase.ConfigureDotnetSdkEnvironment(environment, Path.Combine(dotnetRoot, "dotnet.exe"), "10.0.401");

        Assert.AreSequenceEqual(expected.OrderBy(pair => pair.Key), environment.OrderBy(pair => pair.Key));
    }

    [TestMethod]
    public void PackagedHostFilterTests_AreSelectedByApplicationModelJob()
    {
        string[] categories = typeof(PackagedAppNativeDotnetTestTests)
            .GetCustomAttributes(typeof(TestCategoryAttribute), inherit: false)
            .Cast<TestCategoryAttribute>()
            .SelectMany(attribute => attribute.TestCategories)
            .ToArray();

        Assert.AreSequenceEqual(["WindowsApplicationModel"], categories);
        Assert.ContainsSingle(typeof(PackagedAppNativeDotnetTestTests).GetCustomAttributes(typeof(MemberConditionAttribute), inherit: false));
    }

    [TestMethod]
    [DataRow("MSTest.TestFramework", "lib", "MSTest.TestFramework.dll", false)]
    [DataRow("MSTest.TestAdapter", "lib", "MSTest.TestAdapter.dll", false)]
    [DataRow("Microsoft.Testing.Platform", "lib", "Microsoft.Testing.Platform.dll", true)]
    [DataRow("Microsoft.Testing.Extensions.PackagedApp", "lib", "Microsoft.Testing.Extensions.PackagedApp.dll", true)]
    [DataRow("Microsoft.Testing.Extensions.PackagedApp.MSBuild", "tools/AppModelController", "win-x64/mstest-appmodel-controller.exe", true)]
    [DataRow("Microsoft.Testing.Extensions.PackagedApp.MSBuild", "tools/AppModelController", "win-arm64/mstest-appmodel-controller.exe", true)]
    public void PackedPackages_TargetOnlySupportedNetFrameworks(
        string packageId, string folder, string assembly, bool platformVersion)
    {
        string? version = platformVersion ? AcceptanceTestBase.MicrosoftTestingPlatformVersion : null;
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath(packageId, version));
        string[] entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToArray();

        foreach (string framework in new[] { "net10.0", "net11.0" })
        {
            Assert.Contains($"{folder}/{framework}/{assembly}", entries, packageId);
        }

        Assert.IsEmpty(
            entries.Where(entry => Regex.IsMatch(entry, @"(^|/)net[89]\.0([-/]|$)", RegexOptions.CultureInvariant)).ToArray(),
            $"Package '{packageId}' must not ship .NET 8 or .NET 9 assets.");
    }

    [TestMethod]
    [DataRow("lib/netstandard2.0/MSTest.TestFramework.dll")]
    [DataRow("lib/net10.0/MSTest.TestFramework.dll")]
    [DataRow("lib/net11.0/MSTest.TestFramework.dll")]
    public void PackedMSTestTestFramework_SealsConcreteCoreAttributes(string assemblyPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestFramework"));
        Dictionary<string, TypeAttributes> types = ReadTypeAttributes(archive, assemblyPath);
        const string prefix = "Microsoft.VisualStudio.TestTools.UnitTesting.";
        string[] sealedAttributes =
        [
            nameof(DiscoverInternalsAttribute),
            nameof(DoNotParallelizeAttribute),
            nameof(ParallelizeAttribute),
            nameof(TestDataSourceDiscoveryAttribute),
            nameof(STATestClassAttribute),
        ];

        foreach (string attribute in sealedAttributes)
        {
            Assert.AreEqual(TypeAttributes.Sealed, types[prefix + attribute] & TypeAttributes.Sealed, $"{assemblyPath}: {attribute}");
        }

        Assert.DoesNotContain(prefix + "DataTestMethodAttribute", types.Keys, assemblyPath);
        Assert.AreEqual((TypeAttributes)0, types[prefix + nameof(DataRowAttribute)] & TypeAttributes.Sealed, assemblyPath);
        Assert.AreEqual((TypeAttributes)0, types[prefix + nameof(TestClassAttribute)] & TypeAttributes.Sealed, assemblyPath);
        Assert.AreEqual((TypeAttributes)0, types[prefix + nameof(TestMethodAttribute)] & TypeAttributes.Sealed, assemblyPath);
    }

    [TestMethod]
    [DataRow("lib/netstandard2.0/MSTest.TestFramework.dll", false, true, DisplayName = "DeploymentItem_NetStandard20_IsUnavailable")]
    [DataRow("lib/net10.0/MSTest.TestFramework.dll", true, true, DisplayName = "DeploymentItem_Net10_IsAvailable")]
    [DataRow("lib/net11.0/MSTest.TestFramework.dll", true, true, DisplayName = "DeploymentItem_Net11_IsAvailable")]
    public void PackedMSTestTestFramework_DeploymentItemAvailabilityMatchesTargetFramework(string assemblyPath, bool expectedAvailable, bool directoriesAvailable)
        => AssertCoreContract(assemblyPath, expectedAvailable, directoriesAvailable);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows-specific framework assets are produced only by Windows packs.")]
    [DataRow("lib/net462/MSTest.TestFramework.dll", true, true, DisplayName = "DeploymentItem_Net462_IsAvailable")]
    [DataRow("lib/netstandard2.0/MSTest.TestFramework.dll", false, true, DisplayName = "DeploymentItem_ClassicUwp_IsUnavailable")]
    [DataRow("lib/net10.0/MSTest.TestFramework.dll", true, true, DisplayName = "DeploymentItem_ModernUwp_IsAvailable")]
    [DataRow("lib/net10.0/MSTest.TestFramework.dll", true, true, DisplayName = "DeploymentItem_WinUI_IsAvailable")]
    public void PackedMSTestTestFramework_DeploymentItemAvailabilityMatchesWindowsTargetFramework(string assemblyPath, bool expectedAvailable, bool directoriesAvailable)
        => AssertCoreContract(assemblyPath, expectedAvailable, directoriesAvailable);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    [DataRow("buildTransitive/uap10.0/MSTest.Uwp.dll", false)]
    [DataRow("buildTransitive/net10.0/ui/uwp/MSTest.Uwp.dll", false)]
    [DataRow("buildTransitive/net10.0/ui/winui/MSTest.WinUI.dll", true)]
    public void PackedMSTestTestFramework_SealsConcreteUIAttributes(string assemblyPath, bool winUI)
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestFramework"));
        Dictionary<string, TypeAttributes> types = ReadTypeAttributes(archive, assemblyPath);
        const string prefix = "Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer.";

        Assert.AreEqual(TypeAttributes.Sealed, types[prefix + "UITestMethodAttribute"] & TypeAttributes.Sealed, assemblyPath);
        if (winUI)
        {
            Assert.AreEqual(TypeAttributes.Sealed, types[prefix + "WinUITestTargetAttribute"] & TypeAttributes.Sealed, assemblyPath);
        }
    }

    private static readonly string[] RequiredTestAdapterEntries =
    [
        "lib/net462/MSTest.TestFramework.dll",
        "lib/net462/MSTest.TestAdapter.dll",
        "lib/net462/MSTestAdapter.PlatformServices.dll",
        "lib/net462/fr/MSTest.TestAdapter.resources.dll",
        "lib/net462/fr/MSTestAdapter.PlatformServices.resources.dll",
        "build/net10.0/MSTest.TestAdapter.props",
        "build/net10.0/MSTest.TestAdapter.targets",
        "buildTransitive/net10.0/MSTest.TestAdapter.props",
        "buildTransitive/net10.0/MSTest.TestAdapter.targets",
        "buildTransitive/net10.0/Parallelize.targets",
        "lib/net10.0/MSTest.TestAdapter.dll",
        "lib/net10.0/MSTestAdapter.PlatformServices.dll",
        "lib/net10.0/fr/MSTest.TestAdapter.resources.dll",
        "lib/net10.0/fr/MSTestAdapter.PlatformServices.resources.dll",
        "lib/net11.0/MSTest.TestAdapter.dll",
        "lib/net11.0/MSTestAdapter.PlatformServices.dll",
        "lib/net11.0/fr/MSTest.TestAdapter.resources.dll",
        "lib/net11.0/fr/MSTestAdapter.PlatformServices.resources.dll",
    ];

    private static readonly string[] RequiredTestFrameworkEntries =
    [
        "lib/netstandard2.0/MSTest.TestFramework.dll",
        "lib/net462/MSTest.TestFramework.dll",
        "lib/net10.0/MSTest.TestFramework.dll",
        "lib/net10.0/MSTest.TestFramework.xml",
        "lib/net11.0/MSTest.TestFramework.dll",
        "lib/net10.0/fr/MSTest.TestFramework.resources.dll",
        "lib/net11.0/fr/MSTest.TestFramework.resources.dll",
        "buildTransitive/MSTest.TestFramework.targets",
    ];

    private static readonly string[] RequiredUITestAdapterEntries =
    [
        "build/uap10.0/MSTest.TestAdapter.props",
        "build/uap10.0/MSTest.TestAdapter.targets",
        "buildTransitive/uap10.0/MSTest.TestAdapter.props",
        "buildTransitive/uap10.0/MSTest.TestAdapter.targets",
        "buildTransitive/uap10.0/Parallelize.targets",
        "buildTransitive/uap10.0/MSTest.TestAdapter.dll",
        "buildTransitive/uap10.0/MSTestAdapter.PlatformServices.dll",
        "buildTransitive/uap10.0/_localization/fr/MSTest.TestAdapter.resources.dll",
        "buildTransitive/uap10.0/_localization/fr/MSTestAdapter.PlatformServices.resources.dll",
        "buildTransitive/net10.0/ui/MSTest.TestAdapter.dll",
        "buildTransitive/net10.0/ui/MSTestAdapter.PlatformServices.dll",
    ];

    private static readonly string[] RequiredUITestFrameworkEntries =
    [
        "buildTransitive/net10.0/MSTest.TestFramework.targets",
        "buildTransitive/net10.0/ui/uwp/MSTest.Uwp.dll",
        "buildTransitive/net10.0/ui/uwp/MSTest.Uwp.xml",
        "buildTransitive/net10.0/ui/winui/MSTest.WinUI.dll",
        "buildTransitive/net10.0/ui/winui/MSTest.WinUI.pri",
        "buildTransitive/net10.0/ui/winui/MSTest.WinUI.xml",
        "buildTransitive/net10.0/ui/winui/fr/MSTest.WinUI.resources.dll",
        "buildTransitive/uap10.0/MSTest.TestFramework.targets",
        "buildTransitive/uap10.0/MSTest.Uwp.dll",
        "buildTransitive/uap10.0/MSTest.Uwp.pri",
        "buildTransitive/uap10.0/MSTest.Uwp.xml",
    ];

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_ShipsUIAdaptersOutsideLib()
    {
        string package = GetExactCurrentPackagePath("MSTest.TestAdapter");
        AssertPackageContainsAllEntries(package, [.. RequiredTestAdapterEntries, .. RequiredUITestAdapterEntries]);
        using ZipArchive archive = ZipFile.OpenRead(package);

        // NuGet selects one lib/<tfm> asset per framework, so the UI adapter builds must never become lib assets.
        Assert.DoesNotContain(
            entry => (entry.FullName.StartsWith("lib/", StringComparison.Ordinal)
                    && (entry.FullName.Contains("uap", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("windows", StringComparison.OrdinalIgnoreCase)))
                || entry.FullName.Contains("/winui/", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.Contains("/uwp/", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.Contains("MSTest.Uwp", StringComparison.Ordinal)
                || entry.FullName.Contains("MSTest.WinUI", StringComparison.Ordinal)
                || entry.FullName.Contains("TestFramework.Extensions", StringComparison.Ordinal),
            archive.Entries);
        Assert.DoesNotContain(
            entry => ((entry.FullName.StartsWith("build", StringComparison.Ordinal) && entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    || entry.FullName.Contains("/_localization/", StringComparison.Ordinal))
                && !entry.FullName.StartsWith("buildTransitive/uap10.0/", StringComparison.Ordinal)
                && !entry.FullName.StartsWith("buildTransitive/net10.0/ui/", StringComparison.Ordinal),
            archive.Entries);

        using var reader = new StreamReader(archive.GetEntry("MSTest.TestAdapter.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        string?[] dependencyGroups = nuspec.Descendants()
            .Where(element => element.Name.LocalName == "dependencies")
            .Elements()
            .Select(group => (string?)group.Attribute("targetFramework"))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.AreSequenceEqual([".NETFramework4.6.2", "net10.0", "net11.0", "UAP10.0.16299"], dependencyGroups);

        // The net462 MSTest.TestFramework.dll sidecar must stay out of compile and runtime references, and classic UWP
        // must not get a compile reference to the adapter.
        string[] references = nuspec.Descendants()
            .Where(element => element.Name.LocalName == "references")
            .Elements()
            .SelectMany(group => group.Elements().Select(reference => $"{(string?)group.Attribute("targetFramework")}/{(string?)reference.Attribute("file")}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.AreSequenceEqual(
            [
                "net10.0/MSTest.TestAdapter.dll",
                "net10.0/MSTestAdapter.PlatformServices.dll",
                "net11.0/MSTest.TestAdapter.dll",
                "net11.0/MSTestAdapter.PlatformServices.dll",
                "net462/MSTest.TestAdapter.dll",
                "net462/MSTestAdapter.PlatformServices.dll",
            ],
            references);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestFramework_ShipsUIFrameworksWithoutAdapter()
    {
        string package = GetExactCurrentPackagePath("MSTest.TestFramework");
        AssertPackageContainsAllEntries(package, [.. RequiredTestFrameworkEntries, .. RequiredUITestFrameworkEntries]);
        using ZipArchive archive = ZipFile.OpenRead(package);

        // A shared test library references only MSTest.TestFramework, so the UI payload must not pull in the adapter.
        Assert.DoesNotContain(
            entry => entry.Name is "MSTest.TestAdapter.dll" or "MSTestAdapter.PlatformServices.dll"
                || (entry.FullName.StartsWith("lib/", StringComparison.Ordinal)
                    && (entry.Name.StartsWith("MSTest.Uwp", StringComparison.Ordinal) || entry.Name.StartsWith("MSTest.WinUI", StringComparison.Ordinal))),
            archive.Entries);

        using var reader = new StreamReader(archive.GetEntry("MSTest.TestFramework.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        string?[] dependencyIds = nuspec.Descendants()
            .Where(element => element.Name.LocalName == "dependency")
            .Select(dependency => (string?)dependency.Attribute("id"))
            .Distinct()
            .ToArray();
        Assert.AreSequenceEqual(["MSTest.Analyzers"], dependencyIds);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_ClassicUwpDoesNotDuplicateRuntimeDependencies()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestAdapter"));
        string[] assemblies = archive.Entries
            .Select(entry => entry.FullName)
            .Where(path => path.StartsWith("buildTransitive/uap10.0/", StringComparison.Ordinal)
                && !path["buildTransitive/uap10.0/".Length..].Contains('/')
                && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.AreSequenceEqual(
            [
                "buildTransitive/uap10.0/MSTest.TestAdapter.dll",
                "buildTransitive/uap10.0/MSTestAdapter.PlatformServices.dll",
            ],
            assemblies);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_UIAdapterIsDistinctFromOrdinaryAdapter()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestAdapter"));

        // The ui build carries the UI-thread dispatcher; shipping a copy of lib/net10.0 would silently break [UITestMethod].
        foreach (string assemblyName in new[] { "MSTest.TestAdapter.dll", "MSTestAdapter.PlatformServices.dll" })
        {
            string uiPath = $"buildTransitive/net10.0/ui/{assemblyName}";
            string ordinaryPath = $"lib/net10.0/{assemblyName}";
            ZipArchiveEntry uiEntry = archive.GetEntry(uiPath) ?? throw new AssertFailedException($"MSTest.TestAdapter must contain {uiPath}.");
            ZipArchiveEntry ordinaryEntry = archive.GetEntry(ordinaryPath) ?? throw new AssertFailedException($"MSTest.TestAdapter must contain {ordinaryPath}.");

            using Stream uiStream = uiEntry.Open();
            using Stream ordinaryStream = ordinaryEntry.Open();
            Assert.AreNotEqual(
                Convert.ToHexString(SHA256.HashData(ordinaryStream)),
                Convert.ToHexString(SHA256.HashData(uiStream)),
                $"{uiPath} must be the UI build, not a copy of {ordinaryPath}.");
        }
    }

    [TestMethod]
    [DataRow("Microsoft.Testing.Extensions.TrxReport")]
    [DataRow("Microsoft.Testing.Extensions.Retry")]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Classic UWP package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_ClassicUwpDeclaresBootstrapDependencies(string packageId)
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestAdapter"));
        using var reader = new StreamReader(archive.GetEntry("MSTest.TestAdapter.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        XElement[] groups = nuspec.Descendants().Where(element => element.Name.LocalName == "group" && element.Parent?.Name.LocalName == "dependencies").ToArray();
        XElement classicGroup = groups.Single(group => (string?)group.Attribute("targetFramework") == "UAP10.0.16299");
        XElement dependency = Assert.ContainsSingle(
            classicGroup.Elements().Where(element => (string?)element.Attribute("id") == packageId));

        Assert.AreEqual(AcceptanceTestBase.MicrosoftTestingPlatformVersion, (string?)dependency.Attribute("version"));
        Assert.IsEmpty(
            groups.Where(group => group != classicGroup)
                .SelectMany(group => group.Elements())
                .Where(element => (string?)element.Attribute("id") == packageId)
                .ToArray(),
            $"The classic UWP bootstrap must not add '{packageId}' to other frameworks.");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestAdapter_ClassicUwpTargetsRegisterMSTestBuilderHook()
    {
        string packagePath = GetExactCurrentPackagePath("MSTest.TestAdapter");
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        ZipArchiveEntry targetsEntry = archive.GetEntry("buildTransitive/uap10.0/MSTest.TestAdapter.targets")
            ?? throw new AssertFailedException($"Package '{packagePath}' does not contain the classic UWP targets.");
        using var reader = new StreamReader(targetsEntry.Open());
        string targets = reader.ReadToEnd();

        Assert.Contains("031F8871-2660-4208-8F6B-FC142B40ABFF", targets, targetsEntry.FullName);
        Assert.Contains(
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestingPlatformBuilderHook",
            targets,
            targetsEntry.FullName);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Windows application-model package assets are produced only by Windows packs.")]
    public void PackedMSTestTestFramework_ContainsSharedCoreAssets()
        => AssertPackageContainsAllEntries(
            GetExactCurrentPackagePath("MSTest.TestFramework"),
            RequiredTestFrameworkEntries);

    [TestMethod]
    public void PackedIntegration_AppModelControllerContainsCompleteMtpOnlyRuntime()
    {
        string packagePath = GetExactCurrentPackagePath(
            "Microsoft.Testing.Extensions.PackagedApp.MSBuild",
            AcceptanceTestBase.MicrosoftTestingPlatformVersion);
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        string[] entries = archive.Entries
            .Select(entry => entry.FullName)
            .Where(entry => entry.StartsWith("tools/AppModelController/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.IsEmpty(
            entries.Where(entry => entry.Contains('\\') || entry.Contains("//", StringComparison.Ordinal)).ToArray(),
            "Controller ZIP entries must use canonical single-slash paths on every producer host.");

        string[] targetFrameworks = ["net10.0", "net11.0"];
        (string RuntimeIdentifier, Machine Machine)[] architectures = [("win-x64", Machine.Amd64), ("win-arm64", Machine.Arm64)];
        foreach (string targetFramework in targetFrameworks)
        {
            Assert.DoesNotContain($"tools/AppModelController/{targetFramework}/mstest-appmodel-controller.exe", entries);
            foreach ((string runtimeIdentifier, Machine machine) in architectures)
            {
                string layout = $"tools/AppModelController/{targetFramework}/{runtimeIdentifier}";
                Assert.Contains($"{layout}/mstest-appmodel-controller.exe", entries);
                Assert.Contains($"{layout}/mstest-appmodel-controller.dll", entries);
                Assert.Contains($"{layout}/mstest-appmodel-controller.deps.json", entries);
                Assert.Contains($"{layout}/mstest-appmodel-controller.runtimeconfig.json", entries);
                Assert.Contains($"{layout}/Microsoft.Testing.Platform.dll", entries);
                Assert.Contains($"{layout}/Microsoft.Testing.Extensions.PackagedApp.dll", entries);
                Assert.Contains($"{layout}/fr/Microsoft.Testing.Extensions.PackagedApp.resources.dll", entries);
                Assert.DoesNotContain($"{layout}/mstest-appmodel-controller", entries);

                // The apphost and the native diagnostics reader are the architecture-specific binaries in the layout.
                Assert.AreEqual(machine, GetPortableExecutableMachine(archive.GetEntry($"{layout}/mstest-appmodel-controller.exe")!), $"{layout} apphost");
                Assert.AreEqual(machine, GetPortableExecutableMachine(archive.GetEntry($"{layout}/msdia140.dll")!), $"{layout} msdia140");

                ZipArchiveEntry depsEntry = archive.GetEntry($"{layout}/mstest-appmodel-controller.deps.json")!;
                using var reader = new StreamReader(depsEntry.Open());
                using var deps = System.Text.Json.JsonDocument.Parse(reader.ReadToEnd());
                string runtimeTarget = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
                Assert.EndsWith($"/{runtimeIdentifier}", runtimeTarget);
                foreach (System.Text.Json.JsonProperty library in deps.RootElement.GetProperty("targets").GetProperty(runtimeTarget).EnumerateObject())
                {
                    foreach (System.Text.Json.JsonProperty assetGroup in library.Value.EnumerateObject().Where(property => property.Name is "runtime" or "native" or "runtimeTargets" or "resources"))
                    {
                        foreach (System.Text.Json.JsonProperty asset in assetGroup.Value.EnumerateObject())
                        {
                            string relativePath = assetGroup.Name switch
                            {
                                "resources" => $"{asset.Value.GetProperty("locale").GetString()}/{Path.GetFileName(asset.Name)}",
                                "runtimeTargets" => asset.Name,
                                _ => Path.GetFileName(asset.Name),
                            };
                            Assert.Contains($"{layout}/{relativePath}", entries, $"Missing runtime asset for {library.Name}.");
                        }
                    }
                }
            }
        }

        string[] forbiddenEntries = entries
            .Where(entry =>
                entry.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("MSTest.TestAdapter", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("MSTest.TestFramework", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("vstest", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("UwpTestHostRuntimeProvider", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.IsEmpty(
            forbiddenEntries,
            $"The MTP app-model controller must not carry MSTest or VSTest runtime/deployment assets:{Environment.NewLine}" +
            string.Join(Environment.NewLine, forbiddenEntries));
    }

    [TestMethod]
    public void PackedMSTestSdk_DelegatesToIntegrationWithoutDuplicatePayload()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.Sdk"));
        Assert.DoesNotContain(entry => entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase), archive.Entries);
        Assert.IsNull(archive.GetEntry("Sdk/Runner/ClassicUwpMtpBootstrap.cs"));
        using var reader = new StreamReader(archive.GetEntry("Sdk/Runner/Common.targets")!.Open());
        Assert.Contains("Microsoft.Testing.Extensions.PackagedApp.MSBuild", reader.ReadToEnd());
    }

    [TestMethod]
    public void PackedMSTestAdapter_DoesNotImplicitlyDistributeWindowsController()
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestAdapter"));
        Assert.DoesNotContain(entry => entry.FullName.StartsWith("tools/", StringComparison.OrdinalIgnoreCase), archive.Entries);
        using var reader = new StreamReader(archive.GetEntry("MSTest.TestAdapter.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        Assert.DoesNotContain(
            element => (string?)element.Attribute("id") == "Microsoft.Testing.Extensions.PackagedApp.MSBuild",
            nuspec.Descendants().Where(element => element.Name.LocalName == "dependency"));
    }

    [TestMethod]
    public void PackedIntegration_SharesBuildAssetsAndPinsVersionAlignedDependencies()
    {
        string version = AcceptanceTestBase.MicrosoftTestingPlatformVersion;
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("Microsoft.Testing.Extensions.PackagedApp.MSBuild", version));
        foreach (string folder in new[] { "build", "buildTransitive", "buildMultiTargeting" })
        {
            Assert.IsNotNull(archive.GetEntry($"{folder}/Microsoft.Testing.Extensions.PackagedApp.MSBuild.targets"));
        }

        Assert.IsNotNull(archive.GetEntry("buildTransitive/ClassicUwpMtpBootstrap.cs"));
        Assert.IsNotNull(archive.GetEntry("PACKAGE.md"));
        using var reader = new StreamReader(archive.GetEntry("Microsoft.Testing.Extensions.PackagedApp.MSBuild.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());
        XElement[] groups = nuspec.Descendants().Where(element => element.Name.LocalName == "group").ToArray();
        XElement classicGroup = groups.Single(group => (string?)group.Attribute("targetFramework") == ".NETStandard2.0");
        Assert.HasCount(1, classicGroup.Elements());
        Assert.AreEqual("Microsoft.Testing.Platform.MSBuild", (string?)classicGroup.Elements().Single().Attribute("id"));
        foreach (XElement dependency in groups.SelectMany(group => group.Elements()))
        {
            Assert.AreEqual($"[{version}]", (string?)dependency.Attribute("version"));
        }
    }

    private static string GetExactCurrentPackagePath(string packageId, string? version = null)
    {
        string expectedVersion = version ?? AcceptanceTestBase.MSTestVersion;
        string[] matches = Directory.Exists(Constants.ArtifactsPackagesShipping)
            ? Directory.GetFiles(Constants.ArtifactsPackagesShipping, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly)
                .Where(path => char.IsDigit(Path.GetFileName(path)[packageId.Length + 1])).ToArray()
            : [];
        string expectedPath = Path.Combine(
            Constants.ArtifactsPackagesShipping,
            $"{packageId}.{expectedVersion}.nupkg");

        Assert.HasCount(
            1,
            matches,
            $"Expected exactly one configuration-matched '{packageId}' package in '{Constants.ArtifactsPackagesShipping}', but found {matches.Length}:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, matches.Select(Path.GetFileName))}" +
            $"{Environment.NewLine}Run '.\\build.cmd -pack' for {Constants.BuildConfiguration}; stale packages must not be selected by timestamp.");
        Assert.AreEqual(
            expectedPath,
            matches[0],
            ignoreCase: true,
            $"The only '{packageId}' package does not match the exact locally packed MSTest version '{expectedVersion}'.");
        Assert.IsTrue(File.Exists(expectedPath), $"The expected current packed package '{expectedPath}' does not exist.");
        return expectedPath;
    }

    private static void AssertCoreContract(string assemblyPath, bool deploymentAvailable, bool directoriesAvailable)
    {
        using ZipArchive archive = ZipFile.OpenRead(GetExactCurrentPackagePath("MSTest.TestFramework"));
        Dictionary<string, TypeAttributes> types = ReadTypeAttributes(archive, assemblyPath);
        const string prefix = "Microsoft.VisualStudio.TestTools.UnitTesting.";

        Assert.AreEqual(
            deploymentAvailable,
            types.ContainsKey(prefix + nameof(DeploymentItemAttribute)),
            assemblyPath);
        Assert.Contains(prefix + nameof(TestContext), types.Keys, assemblyPath);
        Assert.Contains(prefix + "ITestClassInstanceFactory", types.Keys, assemblyPath);
        Assert.Contains(prefix + "ITestClassInstanceLease", types.Keys, assemblyPath);

        using Stream stream = archive.GetEntry(assemblyPath)!.Open();
        using var image = new MemoryStream();
        stream.CopyTo(image);
        image.Position = 0;
        using var peReader = new PEReader(image);
        MetadataReader reader = peReader.GetMetadataReader();
        TypeDefinition context = reader.TypeDefinitions.Select(reader.GetTypeDefinition)
            .Single(type => reader.GetString(type.Name) == nameof(TestContext));
        string[] properties = context.GetProperties().Select(reader.GetPropertyDefinition)
            .Select(property => reader.GetString(property.Name)).ToArray();
        string[] directoryProperties =
        [
            nameof(TestContext.TestRunDirectory), nameof(TestContext.DeploymentDirectory),
            nameof(TestContext.ResultsDirectory), nameof(TestContext.TestRunResultsDirectory),
            nameof(TestContext.TestResultsDirectory), nameof(TestContext.TestTempDirectory),
        ];
        Assert.AreSequenceEqual(
            (directoriesAvailable ? directoryProperties : []).Order(StringComparer.Ordinal),
            properties.Intersect(directoryProperties).Order(StringComparer.Ordinal),
            assemblyPath);
        bool frameworkDataAvailable = assemblyPath.StartsWith("lib/net462/", StringComparison.Ordinal);
        Assert.AreEqual(frameworkDataAvailable, properties.Contains("DataRow"), assemblyPath);
        Assert.AreEqual(frameworkDataAvailable, properties.Contains("DataConnection"), assemblyPath);
    }

    private static Dictionary<string, TypeAttributes> ReadTypeAttributes(ZipArchive archive, string assemblyPath)
    {
        ZipArchiveEntry? entry = archive.GetEntry(assemblyPath);
        Assert.IsNotNull(entry, $"The packed framework is missing '{assemblyPath}'.");
        using Stream stream = entry.Open();
        using var image = new MemoryStream();
        stream.CopyTo(image);
        image.Position = 0;
        using var peReader = new PEReader(image);
        MetadataReader reader = peReader.GetMetadataReader();

        return reader.TypeDefinitions
            .Select(reader.GetTypeDefinition)
            .Where(type => (type.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NotPublic)
            .ToDictionary(
                type => $"{reader.GetString(type.Namespace)}.{reader.GetString(type.Name)}",
                type => type.Attributes,
                StringComparer.Ordinal);
    }

    private static Machine GetPortableExecutableMachine(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var image = new MemoryStream();
        stream.CopyTo(image);
        image.Position = 0;
        using var peReader = new PEReader(image);

        return peReader.PEHeaders.CoffHeader.Machine;
    }

    private static void AssertPackageContainsAllEntries(string packagePath, IEnumerable<string> expectedEntries)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        var actualEntries = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] missing = expectedEntries.Where(path => !actualEntries.Contains(path)).ToArray();

        Assert.IsEmpty(
            missing,
            $"Package '{packagePath}' is missing required Windows application-model assets:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing));
    }
}
