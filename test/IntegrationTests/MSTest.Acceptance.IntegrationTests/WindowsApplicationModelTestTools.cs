// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;

namespace MSTest.Acceptance.IntegrationTests;

internal enum WindowsApplicationModelAssetKind
{
    ClassicUwp,
    ModernUwp,
    WinUI,
}

internal sealed record VisualStudioTestTools(string InstallationPath, string MSBuildPath, string VSTestConsolePath);

internal sealed record UwpBuildResult(
    string ProjectPath,
    string BinlogPath,
    string RecipePath,
    string PackageLayoutPath,
    string StandardOutput,
    string ErrorOutput);

internal sealed record UwpRunResult(
    int ExitCode,
    string StandardOutput,
    string ErrorOutput,
    string TrxPath);

/// <summary>
/// Test-only infrastructure for building and executing real Windows application-model assets.
/// </summary>
internal static class WindowsApplicationModelTestTools
{
    private static readonly Version ClassicUwpWindowsSdkVersion = new(10, 0, 16299, 0);
    private static readonly Version MinimumModernUwpWindowsSdkVersion = new(10, 0, 26100, 0);

    public static async Task<VisualStudioTestTools> LocateVisualStudioToolsAsync(CancellationToken cancellationToken)
    {
        string installationPath = await AcceptanceTestBase.FindVisualStudioWithUwpToolsAsync(cancellationToken);
        string msbuildPath = Path.Combine(installationPath, "MSBuild", "Current", "Bin", "MSBuild.exe");
        if (!File.Exists(msbuildPath))
        {
            throw new FileNotFoundException(
                $"Visual Studio at '{installationPath}' satisfies the UWP workload query, but desktop MSBuild.exe was not found at '{msbuildPath}'. " +
                "Repair Microsoft.Component.MSBuild in that installation.",
                msbuildPath);
        }

        string[] vstestCandidates =
        [
            Path.Combine(installationPath, "Common7", "IDE", "Extensions", "TestPlatform", "vstest.console.exe"),
            Path.Combine(installationPath, "Common7", "IDE", "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe"),
        ];
        string vstestConsolePath = vstestCandidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"Visual Studio at '{installationPath}' has MSBuild and the UWP workload, but vstest.console.exe was not found. " +
                $"Install the Visual Studio Test Platform/UWP test tools. Checked:{Environment.NewLine}{string.Join(Environment.NewLine, vstestCandidates)}");

        string testPlatformDirectory = Path.Combine(installationPath, "Common7", "IDE", "Extensions", "TestPlatform");
        string[] uwpRuntimeProviderFiles =
        [
            Path.Combine(testPlatformDirectory, "Extensions", "Microsoft.VisualStudio.UwpTestHostRuntimeProvider.dll"),
            Path.Combine(testPlatformDirectory, "Microsoft.VisualStudio.UwpTestHostRuntimeProvider.Deployment.dll"),
        ];
        string[] missingRuntimeProviderFiles = uwpRuntimeProviderFiles.Where(path => !File.Exists(path)).ToArray();
        return missingRuntimeProviderFiles.Length == 0
            ? new(installationPath, msbuildPath, vstestConsolePath)
            : throw new FileNotFoundException(
                $"Visual Studio 2026 at '{installationPath}' does not contain the VSTest UWP runtime provider required to execute " +
                $".build.appxrecipe files. Install or repair the UWP testing tools. Missing:{Environment.NewLine}" +
                string.Join(Environment.NewLine, missingRuntimeProviderFiles));
    }

    public static async Task<VisualStudioTestTools> LocateModernUwpVisualStudioToolsAsync(CancellationToken cancellationToken)
    {
        VisualStudioTestTools tools = await LocateVisualStudioToolsAsync(cancellationToken);
        var msbuildVersionInfo = FileVersionInfo.GetVersionInfo(tools.MSBuildPath);
        if (msbuildVersionInfo.FileMajorPart < 18)
        {
            throw new InvalidOperationException(
                $"Modern UWP requires Visual Studio 2026 (version 18 or newer), but the UWP-capable installation at " +
                $"'{tools.InstallationPath}' contains MSBuild version '{msbuildVersionInfo.FileVersion}'. Install Visual Studio 2026 " +
                "with the Universal Windows Platform development workload and retry.");
        }

        AssertModernUwpWindowsSdkInstalled();
        return tools;
    }

    public static async Task<VisualStudioTestTools> LocateClassicUwpVisualStudioToolsAsync(CancellationToken cancellationToken)
    {
        VisualStudioTestTools tools = await LocateVisualStudioToolsAsync(cancellationToken);
        AssertClassicUwpWindowsSdkInstalled(tools);
        return tools;
    }

    public static async Task<UwpBuildResult> BuildUwpAssetAsync(
        VisualStudioTestTools tools,
        TestAsset testAsset,
        string projectFileName,
        CancellationToken cancellationToken)
    {
        string projectPath = Path.Combine(testAsset.TargetAssetPath, projectFileName);
        Assert.IsTrue(File.Exists(projectPath), $"The generated UWP project '{projectPath}' does not exist.");

        string[] staleRecipes = Directory.GetFiles(testAsset.TargetAssetPath, "*.build.appxrecipe", SearchOption.AllDirectories);
        Assert.IsEmpty(
            staleRecipes,
            $"The unique UWP test asset '{testAsset.TargetAssetPath}' contained stale .build.appxrecipe files before its build:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, staleRecipes)}");

        string binlogPath = Path.Combine(testAsset.TargetAssetPath, $"{Path.GetFileNameWithoutExtension(projectFileName)}.binlog");
        BoundedCommandLineResult result = await AcceptanceTestBase.RunWindowsApplicationModelCommandAsync(
            $"\"{tools.MSBuildPath}\" \"{projectPath}\" /restore /t:Build /p:Configuration=Release /p:Platform=x64 /warnaserror /bl:\"{binlogPath}\"",
            testAsset.TargetAssetPath,
            cancellationToken);
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"Building the real UWP asset failed. Project: '{projectPath}'. Binlog: '{binlogPath}'.{Environment.NewLine}" +
            $"Standard output:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
            $"Standard error:{Environment.NewLine}{result.ErrorOutput}");

        string[] recipes = Directory.GetFiles(testAsset.TargetAssetPath, "*.build.appxrecipe", SearchOption.AllDirectories);
        Assert.HasCount(
            1,
            recipes,
            $"Expected exactly one tooling-generated .build.appxrecipe beneath '{testAsset.TargetAssetPath}', but found {recipes.Length}:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, recipes)}{Environment.NewLine}Binlog: '{binlogPath}'.");

        string recipePath = recipes[0];
        string[] generatedManifests = Directory.GetFiles(testAsset.TargetAssetPath, "AppxManifest.xml", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Contains("obj", StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.IsNotEmpty(
            generatedManifests,
            $"The UWP build produced recipe '{recipePath}' but no tooling-generated AppxManifest.xml package layout. Binlog: '{binlogPath}'.");

        string recipeDirectory = Path.GetDirectoryName(recipePath)!;
        string generatedManifest = generatedManifests
            .OrderByDescending(path => GetCommonPathPrefixLength(recipeDirectory, path))
            .First();
        string packageLayoutPath = Path.GetDirectoryName(generatedManifest)!;

        return new(projectPath, binlogPath, recipePath, packageLayoutPath, result.StandardOutput, result.ErrorOutput);
    }

    public static async Task<UwpRunResult> RunUwpRecipeAsync(
        VisualStudioTestTools tools,
        string recipePath,
        string resultsDirectory,
        CancellationToken cancellationToken)
    {
        Assert.IsTrue(File.Exists(recipePath), $"The UWP recipe '{recipePath}' does not exist.");
        Directory.CreateDirectory(resultsDirectory);
        string trxFileName = $"uwp-{Guid.NewGuid():N}.trx";
        string trxPath = Path.Combine(resultsDirectory, trxFileName);

        BoundedCommandLineResult result = await AcceptanceTestBase.RunWindowsApplicationModelCommandAsync(
            $"\"{tools.VSTestConsolePath}\" \"{recipePath}\" /Logger:\"trx;LogFileName={trxFileName}\" /ResultsDirectory:\"{resultsDirectory}\" /Platform:x64 /Framework:FrameworkUap10",
            Path.GetDirectoryName(recipePath),
            cancellationToken);

        return new(result.ExitCode, result.StandardOutput, result.ErrorOutput, trxPath);
    }

    public static void AssertResolvedMSTestAssets(string reportPath, WindowsApplicationModelAssetKind assetKind)
    {
        Assert.IsTrue(File.Exists(reportPath), $"The resolved MSTest asset report '{reportPath}' does not exist.");
        string report = File.ReadAllText(reportPath).Replace('\\', '/');

        (string[] Required, string[] Rejected) assetPaths = assetKind switch
        {
            WindowsApplicationModelAssetKind.ClassicUwp =>
            (
                [
                    "/buildTransitive/uap10.0/MSTest.TestAdapter.dll",
                    "/buildTransitive/uap10.0/MSTestAdapter.PlatformServices.dll",
                    "/lib/uap10.0/MSTest.TestFramework.dll",
                    "/lib/uap10.0/MSTest.TestFramework.Extensions.dll",
                ],
                [
                    "/lib/netstandard2.0/MSTest.TestFramework.dll",
                    "/lib/netstandard2.0/MSTest.TestFramework.Extensions.dll",
                ]),
            WindowsApplicationModelAssetKind.ModernUwp =>
            (
                [
                    "/lib/net9.0/MSTest.TestFramework.dll",
                    "/buildTransitive/net9.0/uwp/MSTest.TestAdapter.dll",
                    "/buildTransitive/net9.0/uwp/MSTestAdapter.PlatformServices.dll",
                    "/buildTransitive/net9.0/uwp/MSTest.TestFramework.Extensions.dll",
                ],
                [
                    "/buildTransitive/net9.0/MSTest.TestAdapter.dll",
                    "/buildTransitive/net9.0/MSTestAdapter.PlatformServices.dll",
                    "/buildTransitive/net9.0/MSTest.TestFramework.Extensions.dll",
                ]),
            WindowsApplicationModelAssetKind.WinUI =>
            (
                [
                    "/buildTransitive/net8.0/winui/MSTest.TestAdapter.dll",
                    "/buildTransitive/net8.0/winui/MSTestAdapter.PlatformServices.dll",
                    "/buildTransitive/net8.0/winui/MSTest.TestFramework.Extensions.dll",
                ],
                [
                    "/buildTransitive/net8.0/MSTest.TestAdapter.dll",
                    "/buildTransitive/net8.0/MSTestAdapter.PlatformServices.dll",
                    "/buildTransitive/net8.0/MSTest.TestFramework.Extensions.dll",
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, "Unknown Windows application model."),
        };

        string[] required = assetPaths.Required;
        string[] rejected = assetPaths.Rejected;
        if (assetKind == WindowsApplicationModelAssetKind.ModernUwp)
        {
            Assert.Contains(
                "UseUwpTools=true",
                report,
                $"The resolved MSTest report '{reportPath}' does not prove that the Modern UWP SDK enabled UseUwpTools.");
        }
        else if (assetKind == WindowsApplicationModelAssetKind.ClassicUwp)
        {
            Assert.Contains(
                "TargetFramework=uap10.0.16299",
                report,
                $"The resolved MSTest report '{reportPath}' does not prove the classic UWP consumer targeted uap10.0.16299.");
            Assert.Contains(
                "MSBuildRuntimeType=Full",
                report,
                $"The resolved MSTest report '{reportPath}' does not prove the classic UWP consumer used desktop MSBuild.");
        }

        string[] missing = required.Where(path => !report.Contains(path, StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] unexpectedlyResolved = rejected.Where(path => report.Contains(path, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.IsEmpty(
            missing,
            $"The resolved MSTest report '{reportPath}' is missing the following {assetKind} assets:{Environment.NewLine}" +
            string.Join(Environment.NewLine, missing));
        Assert.IsEmpty(
            unexpectedlyResolved,
            $"The resolved MSTest report '{reportPath}' selected ordinary assets instead of specialized {assetKind} assets:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, unexpectedlyResolved)}");
    }

    public static void CopySampleAssets(string targetAssetPath)
    {
        string sourceAssets = Path.Combine(Constants.Root, "samples", "public", "UwpVSTestApp", "Assets");
        Assert.IsTrue(Directory.Exists(sourceAssets), $"Canonical UWP sample assets were not found at '{sourceAssets}'.");

        string destinationAssets = Path.Combine(targetAssetPath, "Assets");
        Directory.CreateDirectory(destinationAssets);
        foreach (string sourceFile in Directory.GetFiles(sourceAssets, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(destinationAssets, Path.GetFileName(sourceFile)), overwrite: true);
        }
    }

    public static async Task ExecuteWithPackageCleanupAsync(
        TestAsset testAsset,
        string packageIdentityName,
        Func<Task> testAction)
    {
        Exception? testFailure = null;
        Exception? cleanupFailure = null;

        try
        {
            await testAction();
        }
        catch (Exception ex)
        {
            testFailure = ex;
        }

        try
        {
            await RemoveAndVerifyPackageRegistrationAsync(packageIdentityName);
        }
        catch (Exception ex)
        {
            cleanupFailure = ex;
        }

        if (cleanupFailure is null)
        {
            testAsset.Dispose();
        }

        if (testFailure is not null && cleanupFailure is not null)
        {
            throw new AggregateException(
                $"The UWP acceptance test failed and cleanup also failed for identity '{packageIdentityName}'. The package layout was retained.",
                testFailure,
                cleanupFailure);
        }

        if (cleanupFailure is not null)
        {
            throw new AggregateException(
                $"Cleanup failed for UWP identity '{packageIdentityName}'. The package layout was retained.",
                cleanupFailure);
        }

        if (testFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(testFailure).Throw();
        }
    }

    private static async Task RemoveAndVerifyPackageRegistrationAsync(string packageIdentityName)
    {
        string escapedIdentity = packageIdentityName.Replace("'", "''", StringComparison.Ordinal);
        string script =
            $"$packages = @(Get-AppxPackage -Name '{escapedIdentity}' -ErrorAction Stop | " +
            $"Where-Object {{ $_.Name -ceq '{escapedIdentity}' }}); " +
            "foreach ($package in $packages) { Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop }; " +
            $"$remaining = @(Get-AppxPackage -Name '{escapedIdentity}' -ErrorAction Stop | " +
            $"Where-Object {{ $_.Name -ceq '{escapedIdentity}' }}); " +
            "if ($remaining.Count -ne 0) { throw \"Package registration still exists after cleanup: $($remaining.PackageFullName -join ', ')\" }; " +
            "Write-Output 'WINDOWS_APP_MODEL_REGISTRATION_ABSENT'";
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        BoundedCommandLineResult result = await AcceptanceTestBase.RunWindowsApplicationModelCommandAsync(
            $"powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            workingDirectory: null,
            cancellationToken: CancellationToken.None);

        Assert.AreEqual(
            0,
            result.ExitCode,
            $"Failed to remove or verify removal of UWP identity '{packageIdentityName}'." +
            $"{Environment.NewLine}Standard output:{Environment.NewLine}{result.StandardOutput}" +
            $"{Environment.NewLine}Standard error:{Environment.NewLine}{result.ErrorOutput}");
        Assert.Contains(
            "WINDOWS_APP_MODEL_REGISTRATION_ABSENT",
            result.StandardOutput,
            $"Cleanup did not verify that UWP identity '{packageIdentityName}' is absent.");
    }

    private static int GetCommonPathPrefixLength(string first, string second)
    {
        int length = Math.Min(first.Length, second.Length);
        int index = 0;
        while (index < length && char.ToUpperInvariant(first[index]) == char.ToUpperInvariant(second[index]))
        {
            index++;
        }

        return index;
    }

    private static void AssertClassicUwpWindowsSdkInstalled(VisualStudioTestTools tools)
    {
        string windowsKitsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits",
            "10");
        string sdkVersion = ClassicUwpWindowsSdkVersion.ToString();
        string[] requiredSdkFiles =
        [
            Path.Combine(windowsKitsRoot, "bin", sdkVersion, "x64", "makeappx.exe"),
            Path.Combine(windowsKitsRoot, "Platforms", "UAP", sdkVersion, "Platform.xml"),
            Path.Combine(
                windowsKitsRoot,
                "References",
                sdkVersion,
                "Windows.Foundation.FoundationContract",
                "3.0.0.0",
                "Windows.Foundation.FoundationContract.winmd"),
            Path.Combine(windowsKitsRoot, "UnionMetadata", sdkVersion, "Windows.winmd"),
        ];
        string[] missingSdkFiles = requiredSdkFiles.Where(path => !File.Exists(path)).ToArray();
        if (missingSdkFiles.Length != 0)
        {
            throw new FileNotFoundException(
                $"Classic UWP requires Windows SDK {ClassicUwpWindowsSdkVersion} and its x64 packaging tools. " +
                $"Install that SDK through the Visual Studio installer. Missing:{Environment.NewLine}" +
                string.Join(Environment.NewLine, missingSdkFiles));
        }

        var msbuildVersionInfo = FileVersionInfo.GetVersionInfo(tools.MSBuildPath);
        string testPlatformUniversalVersion = $"{msbuildVersionInfo.FileMajorPart}.0";
        string testPlatformUniversalManifest = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft SDKs",
            "Windows Kits",
            "10",
            "ExtensionSDKs",
            "TestPlatform.Universal",
            testPlatformUniversalVersion,
            "SDKManifest.xml");
        if (!File.Exists(testPlatformUniversalManifest))
        {
            throw new FileNotFoundException(
                $"Classic UWP execution requires the TestPlatform.Universal {testPlatformUniversalVersion} extension SDK matching " +
                $"desktop MSBuild '{tools.MSBuildPath}', but its manifest was not found at '{testPlatformUniversalManifest}'. " +
                "Install or repair the Visual Studio UWP testing tools.",
                testPlatformUniversalManifest);
        }
    }

    private static void AssertModernUwpWindowsSdkInstalled()
    {
        string windowsKitsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits",
            "10");
        string sdkBinRoot = Path.Combine(windowsKitsRoot, "bin");
        if (!Directory.Exists(sdkBinRoot))
        {
            throw new DirectoryNotFoundException(
                $"Modern UWP requires Windows SDK {MinimumModernUwpWindowsSdkVersion} or newer, but the Windows SDK bin directory " +
                $"'{sdkBinRoot}' does not exist. Install that SDK through the Visual Studio 2026 installer.");
        }

        Version[] installedSdkVersions = Directory.GetDirectories(sdkBinRoot)
            .Select(Path.GetFileName)
            .Select(name => Version.TryParse(name, out Version? version) ? version : null)
            .Where(version => version is not null)
            .Cast<Version>()
            .OrderDescending()
            .ToArray();
        _ = installedSdkVersions.FirstOrDefault(version =>
            version >= MinimumModernUwpWindowsSdkVersion
            && GetMissingModernUwpSdkFiles(windowsKitsRoot, sdkBinRoot, version).Length == 0)
            ?? throw new InvalidOperationException(
                $"Modern UWP requires a complete Windows SDK {MinimumModernUwpWindowsSdkVersion} or newer beneath '{sdkBinRoot}', but found: " +
                $"{(installedSdkVersions.Length == 0 ? "<none>" : string.Join(", ", installedSdkVersions))}. " +
                "Install a current Windows 11 SDK through the Visual Studio 2026 installer.");
    }

    private static string[] GetMissingModernUwpSdkFiles(string windowsKitsRoot, string sdkBinRoot, Version sdkVersion)
    {
        string version = sdkVersion.ToString();
        string[] requiredFiles =
        [
            Path.Combine(sdkBinRoot, version, "x64", "makeappx.exe"),
            Path.Combine(sdkBinRoot, version, "x64", "makepri.exe"),
            Path.Combine(windowsKitsRoot, "Platforms", "UAP", version, "Platform.xml"),
            Path.Combine(windowsKitsRoot, "UnionMetadata", version, "Windows.winmd"),
        ];
        return requiredFiles.Where(path => !File.Exists(path)).ToArray();
    }
}
