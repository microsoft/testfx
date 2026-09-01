// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// Exercises packaged MTP WinUI launch through both Microsoft.Testing.Extensions.PackagedApp
/// and the independently versioned WinApp CLI interoperability boundary.
/// </summary>
[TestClass]
[TestCategory("WindowsApplicationModel")]
[MemberCondition(
    typeof(AcceptanceTestBase),
    nameof(AcceptanceTestBase.IsWindowsApplicationModelTestEnvironment),
    IgnoreMessage = "Requires the dedicated preflight-gated Windows application-model test environment.")]
[OSCondition(OperatingSystems.Windows, IgnoreMessage = "Packaged WinUI activation is supported only on Windows.")]
[DoNotParallelize]
public sealed class PackagedWinUITests : AcceptanceTestBase<NopAssetFixture>
{
    private const string TargetFramework = "net8.0-windows10.0.19041.0";
    private const string RuntimeIdentifier = "win-x64";
    private const string Publisher = "CN=MSTestAcceptance";
    private const string LauncherModeEnvironmentVariable = "TESTINGPLATFORM_PACKAGEDAPP_LAUNCHER";
    private const string WinAppCliPathEnvironmentVariable = "TESTFX_WINAPP_CLI_PATH";

    [TestMethod]
    public async Task PackagedFullTrustWinUI_RegistersActivatesConnectsBackRunsUiTestsAndCleansUp()
    {
        GeneratedPackagedWinUIAsset generatedAsset = await GeneratePackagedWinUIAssetAsync();
        await ExecuteWithPackageCleanupAsync(
            generatedAsset,
            async () =>
            {
                PackagedWinUIBuild build = await BuildAssetAsync(
                    generatedAsset.TestAsset,
                    generatedAsset.AssetName,
                    generatedAsset.PackageIdentityName);
                AssertResolvedWinUIAssets(build.ResolvedAssetsReportPath);

                var testHost = TestInfrastructure.TestHost.LocateFrom(build.PackageLayoutPath, generatedAsset.AssetName);
                TestHostResult testHostResult = await ExecuteBoundedAsync(
                    testHost,
                    environmentVariables: new Dictionary<string, string?>
                    {
                        // Make the environment deterministic: an inherited override must not turn this into the
                        // forced loose-layout path. The AppxManifest.xml probe has to enable the launcher.
                        [LauncherModeEnvironmentVariable] = null,
                    });

                testHostResult.AssertExitCodeIs(ExitCode.Success);
                AssertExecutedTestsMarker(generatedAsset.ExecutionMarkerPath);

                AssertActivatedIdentityMarker(
                    generatedAsset.IdentityMarkerPath,
                    generatedAsset.PackageIdentityName,
                    generatedAsset.ExpectedPackageFamilyName,
                    build.PackageLayoutPath);
                await AssertPackageIsRegisteredAsync(
                    generatedAsset.PackageIdentityName,
                    generatedAsset.ExpectedPackageFamilyName,
                    build.PackageLayoutPath);
            });
    }

    [TestMethod]
    [MemberCondition(
        typeof(AcceptanceTestBase),
        nameof(AcceptanceTestBase.IsWinAppCliInteropTestEnvironment),
        IgnoreMessage = "Requires the CI-pinned WinApp CLI interoperability environment.")]
    public async Task WinAppCli_RegistersActivatesAndRunsPackagedMtpWinUIApp()
    {
        GeneratedPackagedWinUIAsset generatedAsset = await GeneratePackagedWinUIAssetAsync();
        await ExecuteWithPackageCleanupAsync(
            generatedAsset,
            async () =>
            {
                PackagedWinUIBuild build = await BuildAssetAsync(
                    generatedAsset.TestAsset,
                    generatedAsset.AssetName,
                    generatedAsset.PackageIdentityName,
                    enablePackagedAppExtension: false);
                AssertWinAppCliInteropAssets(build.ResolvedAssetsReportPath);

                string winAppCliPath = Environment.GetEnvironmentVariable(WinAppCliPathEnvironmentVariable)
                    ?? throw new InvalidOperationException(
                        $"{WinAppCliPathEnvironmentVariable} must point to the CI-pinned winapp.exe.");
                Assert.IsTrue(File.Exists(winAppCliPath), $"WinApp CLI was not found at '{winAppCliPath}'.");

                string winAppLayoutPath = Path.Combine(generatedAsset.TestAsset.TargetAssetPath, "WinAppCliLayout");
                BoundedCommandLineResult result = await RunWindowsApplicationModelCommandAsync(
                    $"\"{winAppCliPath}\" run \"{build.PackageLayoutPath}\" " +
                    $"--manifest \"{build.GeneratedManifestPath}\" " +
                    $"--output-appx-directory \"{winAppLayoutPath}\" --unregister-on-exit --json",
                    generatedAsset.TestAsset.TargetAssetPath,
                    TestContext.CancellationToken);
                Assert.AreEqual(
                    0,
                    result.ExitCode,
                    $"WinApp CLI failed to register and launch the packaged MTP WinUI app." +
                    $"{Environment.NewLine}Standard output:{Environment.NewLine}{result.StandardOutput}" +
                    $"{Environment.NewLine}Standard error:{Environment.NewLine}{result.ErrorOutput}" +
                    $"{Environment.NewLine}Build binlog: '{build.BinlogPath}'.");

                int jsonStart = result.StandardOutput.IndexOf('{');
                int jsonEnd = result.StandardOutput.LastIndexOf('}');
                Assert.IsGreaterThanOrEqualTo(0, jsonStart, result.StandardOutput);
                Assert.IsGreaterThan(jsonStart, jsonEnd, result.StandardOutput);
                using var output = JsonDocument.Parse(result.StandardOutput[jsonStart..(jsonEnd + 1)]);
                JsonElement root = output.RootElement;
                Assert.AreEqual(
                    $"{generatedAsset.ExpectedPackageFamilyName}!App",
                    root.GetProperty("AUMID").GetString(),
                    result.StandardOutput);
                Assert.IsGreaterThan(0u, root.GetProperty("ProcessId").GetUInt32(), result.StandardOutput);

                AssertExecutedTestsMarker(generatedAsset.ExecutionMarkerPath);
                AssertActivatedIdentityMarker(
                    generatedAsset.IdentityMarkerPath,
                    generatedAsset.PackageIdentityName,
                    generatedAsset.ExpectedPackageFamilyName,
                    winAppLayoutPath);
            });
    }

    public TestContext TestContext { get; set; } = null!;

    private async Task<GeneratedPackagedWinUIAsset> GeneratePackagedWinUIAssetAsync()
    {
        string uniqueSuffix = Guid.NewGuid().ToString("N");
        // The Windows App SDK still runs the .NET Framework XamlCompiler.exe. Keep the generated
        // project, root namespace, and assembly name short enough that its intermediatexaml assembly
        // stays below MAX_PATH; otherwise MarkupCompilePass2 exits with code 1 and no diagnostic.
        const string AssetName = "PackagedWinUI";
        string packageIdentityName = $"MTPWinUI{uniqueSuffix}";
        string expectedPackageFamilyName = ComputePackageFamilyName(packageIdentityName, Publisher);
        string phoneProductId = Guid.NewGuid().ToString();
        TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
                .PatchCodeWithReplace("$AssetName$", AssetName)
                .PatchCodeWithReplace("$PackageIdentityName$", packageIdentityName)
                .PatchCodeWithReplace("$ExpectedPackageFamilyName$", expectedPackageFamilyName)
                .PatchCodeWithReplace("$Publisher$", Publisher)
                .PatchCodeWithReplace("$PhoneProductId$", phoneProductId)
                .PatchCodeWithReplace("$TargetFramework$", TargetFramework)
                .PatchCodeWithReplace("$RuntimeIdentifier$", RuntimeIdentifier)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$WindowsAppSdkVersion$", WindowsAppSdkPackageVersion)
                .PatchCodeWithReplace("$WindowsSdkBuildToolsVersion$", WindowsSdkBuildToolsPackageVersion));

        try
        {
            string identityMarkerPath = Path.Combine(testAsset.TargetAssetPath, "activated-package-identity.txt");
            string executionMarkerPath = Path.Combine(testAsset.TargetAssetPath, "executed-tests.txt");
            PatchMarkerPaths(testAsset.TargetAssetPath, identityMarkerPath, executionMarkerPath);
            CopyPackagedWinUISampleAssets(testAsset.TargetAssetPath);
            await EnsureGeneratedCSharpFilesHaveUtf8BomAsync(testAsset.TargetAssetPath, TestContext.CancellationToken);

            return new(
                AssetName,
                testAsset,
                identityMarkerPath,
                packageIdentityName,
                expectedPackageFamilyName,
                executionMarkerPath);
        }
        catch
        {
            testAsset.Dispose();
            throw;
        }
    }

    private static async Task ExecuteWithPackageCleanupAsync(
        GeneratedPackagedWinUIAsset generatedAsset,
        Func<Task> testAction)
    {
        Exception? testFailure = null;
        Exception? cleanupFailure = null;
        bool cleanupSucceeded = false;

        try
        {
            await testAction();
        }
        catch (Exception ex)
        {
            testFailure = ex;
        }
        finally
        {
            try
            {
                await RemoveAndVerifyPackageRegistrationAsync(generatedAsset.PackageIdentityName);
                cleanupSucceeded = true;
            }
            catch (Exception ex)
            {
                cleanupFailure = ex;
            }

            // A registered loose package must never point at a directory that this test has deleted. Keep
            // the layout for diagnosis when cleanup failed; otherwise disposal is safe even after a test
            // assertion failed.
            if (cleanupSucceeded)
            {
                generatedAsset.TestAsset.Dispose();
            }
        }

        if (testFailure is not null && cleanupFailure is not null)
        {
            throw new AggregateException(
                $"The packaged WinUI test failed and cleanup also failed for identity '{generatedAsset.PackageIdentityName}'. " +
                "The loose layout was retained.",
                testFailure,
                cleanupFailure);
        }

        if (cleanupFailure is not null)
        {
            throw new AggregateException(
                $"Cleanup failed for packaged WinUI identity '{generatedAsset.PackageIdentityName}'. The loose layout was retained.",
                cleanupFailure);
        }

        if (testFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(testFailure).Throw();
        }
    }

    private async Task<PackagedWinUIBuild> BuildAssetAsync(
        TestAsset testAsset,
        string assetName,
        string packageIdentityName,
        bool enablePackagedAppExtension = true)
    {
        string projectPath = Path.Combine(testAsset.TargetAssetPath, $"{assetName}.csproj");
        string packagedAppProperty = enablePackagedAppExtension
            ? string.Empty
            : " -p:EnableMicrosoftTestingExtensionsPackagedApp=false";
        DotnetMuxerResult buildResult = await DotnetCli.RunAsync(
            $"build \"{projectPath}\" -c Release -p:Platform=x64 -p:RuntimeIdentifier={RuntimeIdentifier} " +
            $"-p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=false{packagedAppProperty}",
            workingDirectory: testAsset.TargetAssetPath,
            failIfReturnValueIsNotZero: false,
            // The PRI tooling can report warnings for third-party localized resource assemblies. Those
            // warnings are unrelated to the app-model behavior under test.
            warnAsError: false,
            cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(
            0,
            buildResult.ExitCode,
            $"Building the real packaged WinUI asset failed. Project: '{projectPath}'. Binlog: '{buildResult.BinlogPath}'." +
            $"{Environment.NewLine}{buildResult}");

        string packageLayoutPath = Path.Combine(
            testAsset.TargetAssetPath,
            "bin",
            "x64",
            "Release",
            TargetFramework,
            RuntimeIdentifier);
        Assert.IsTrue(
            Directory.Exists(packageLayoutPath),
            $"The WinUI/MSIX tooling did not create the expected loose package layout '{packageLayoutPath}'. " +
            $"Binlog: '{buildResult.BinlogPath}'.");

        string sourceManifestPath = Path.Combine(testAsset.TargetAssetPath, "Package.appxmanifest");
        string generatedManifestPath = Path.Combine(packageLayoutPath, "AppxManifest.xml");
        Assert.IsTrue(File.Exists(sourceManifestPath), $"The generated asset is missing its source manifest '{sourceManifestPath}'.");
        Assert.IsTrue(
            File.Exists(generatedManifestPath),
            $"The real WinUI/MSIX build did not produce the tooling-generated manifest '{generatedManifestPath}'. " +
            $"A copied source manifest is not accepted as packaged WinUI coverage. Binlog: '{buildResult.BinlogPath}'.");
        Assert.AreNotEqual(
            Path.GetFullPath(sourceManifestPath),
            Path.GetFullPath(generatedManifestPath),
            "The package layout must use the AppxManifest.xml generated by MSIX tooling, not the source Package.appxmanifest.");

        AssertFullTrustGeneratedManifest(
            generatedManifestPath,
            packageLayoutPath,
            assetName,
            packageIdentityName);

        string resolvedAssetsReportPath = Path.Combine(testAsset.TargetAssetPath, "resolved-mstest-assets.txt");
        return new(packageLayoutPath, generatedManifestPath, resolvedAssetsReportPath, buildResult.BinlogPath!);
    }

    private static void AssertFullTrustGeneratedManifest(
        string generatedManifestPath,
        string packageLayoutPath,
        string assetName,
        string packageIdentityName)
    {
        var manifest = XDocument.Load(generatedManifestPath);
        XElement root = manifest.Root
            ?? throw new AssertFailedException($"The generated manifest '{generatedManifestPath}' has no Package root.");
        XElement identity = root.Elements().Single(element => element.Name.LocalName == "Identity");
        Assert.AreEqual(
            packageIdentityName,
            (string?)identity.Attribute("Name"),
            $"Unexpected package identity in generated manifest '{generatedManifestPath}'.");
        Assert.AreEqual(Publisher, (string?)identity.Attribute("Publisher"), generatedManifestPath);
        Assert.AreEqual("x64", (string?)identity.Attribute("ProcessorArchitecture"), generatedManifestPath);

        XElement application = root
            .Descendants()
            .Single(element => element.Name.LocalName == "Application");
        string executable = (string?)application.Attribute("Executable")
            ?? throw new AssertFailedException($"The generated manifest '{generatedManifestPath}' does not declare an executable.");
        Assert.AreEqual($"{assetName}.exe", executable, ignoreCase: true, generatedManifestPath);
        Assert.AreEqual(
            "Windows.FullTrustApplication",
            (string?)application.Attribute("EntryPoint"),
            $"The generated manifest '{generatedManifestPath}' must describe a full-trust desktop application.");

        string executablePath = Path.Combine(packageLayoutPath, executable.Replace('\\', Path.DirectorySeparatorChar));
        Assert.IsTrue(
            File.Exists(executablePath),
            $"The generated manifest declares '{executable}', but the matching real executable does not exist at '{executablePath}'.");
        Assert.IsNotEmpty(
            root.Descendants().Where(element =>
                element.Name.LocalName == "Capability"
                && string.Equals((string?)element.Attribute("Name"), "runFullTrust", StringComparison.Ordinal)).ToArray(),
            $"The generated manifest '{generatedManifestPath}' does not retain rescap:Capability Name=\"runFullTrust\".");
    }

    private static void AssertResolvedWinUIAssets(string reportPath)
    {
        Assert.IsTrue(File.Exists(reportPath), $"The resolved WinUI asset report '{reportPath}' does not exist.");
        string report = File.ReadAllText(reportPath).Replace('\\', '/');
        Assert.Contains("UseWinUI=true", report, reportPath);
        Assert.Contains($"TargetFramework={TargetFramework}", report, reportPath);
        Assert.Contains("UsingMSTestSdk=true", report, reportPath);
        Assert.Contains("EnableMSTestRunner=true", report, reportPath);
        Assert.Contains("GenerateTestingPlatformEntryPoint=false", report, reportPath);
        Assert.Contains("GenerateTestingPlatformApplicationHelper=true", report, reportPath);
        Assert.Contains("EnableMicrosoftTestingExtensionsPackagedApp=true", report, reportPath);

        string[] required =
        [
            "/buildTransitive/net8.0/winui/MSTest.TestAdapter.dll",
            "/buildTransitive/net8.0/winui/MSTestAdapter.PlatformServices.dll",
            "/buildTransitive/net8.0/winui/MSTest.TestFramework.Extensions.dll",
            "/lib/net8.0-windows10.0.19041/Microsoft.Testing.Extensions.PackagedApp.dll",
        ];
        string[] rejected =
        [
            "/buildTransitive/net8.0/MSTest.TestAdapter.dll",
            "/buildTransitive/net8.0/MSTestAdapter.PlatformServices.dll",
            "/buildTransitive/net8.0/MSTest.TestFramework.Extensions.dll",
            "/lib/net8.0/Microsoft.Testing.Extensions.PackagedApp.dll",
        ];

        string[] missing = required.Where(path => !report.Contains(path, StringComparison.OrdinalIgnoreCase)).ToArray();
        string[] unexpectedlyResolved = rejected.Where(path => report.Contains(path, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.IsEmpty(
            missing,
            $"The packaged WinUI consumer did not resolve all required net8.0/winui and Windows packaged-app assets:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, missing)}{Environment.NewLine}Report: '{reportPath}'.");
        Assert.IsEmpty(
            unexpectedlyResolved,
            $"The packaged WinUI consumer resolved ordinary assets instead of its specialized WinUI/Windows assets:" +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, unexpectedlyResolved)}{Environment.NewLine}Report: '{reportPath}'.");
    }

    private static void AssertWinAppCliInteropAssets(string reportPath)
    {
        Assert.IsTrue(File.Exists(reportPath), $"The resolved WinUI asset report '{reportPath}' does not exist.");
        string report = File.ReadAllText(reportPath).Replace('\\', '/');
        Assert.Contains("UseWinUI=true", report, reportPath);
        Assert.Contains("EnableMSTestRunner=true", report, reportPath);
        Assert.Contains("EnableMicrosoftTestingExtensionsPackagedApp=false", report, reportPath);
        Assert.DoesNotContain(
            "/Microsoft.Testing.Extensions.PackagedApp.dll",
            report,
            $"The WinApp CLI interoperability app must not use the testfx packaged-app launcher. Report: '{reportPath}'.");
    }

    private async Task<TestHostResult> ExecuteBoundedAsync(
        TestInfrastructure.TestHost testHost,
        Dictionary<string, string?> environmentVariables)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(WindowsApplicationModelExecutionTimeout);
        try
        {
            return await testHost.ExecuteAsync(
                environmentVariables: environmentVariables,
                cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !TestContext.CancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The full-trust packaged WinUI test host did not exit within {WindowsApplicationModelExecutionTimeout}. " +
                "The package may have failed during registration/AUMID activation, WinUI startup, or MTP connect-back.");
        }
    }

    private static void AssertActivatedIdentityMarker(
        string markerPath,
        string packageIdentityName,
        string expectedPackageFamilyName,
        string packageLayoutPath)
    {
        Assert.IsTrue(
            File.Exists(markerPath),
            $"The activated test did not report its package identity to '{markerPath}'.");
        string[] marker = File.ReadAllText(markerPath).Trim().Split('|');
        Assert.HasCount(3, marker, $"Unexpected activated identity marker content in '{markerPath}'.");
        Assert.AreEqual(packageIdentityName, marker[0], $"Unexpected Package.Current.Id.Name in '{markerPath}'.");
        Assert.AreEqual(expectedPackageFamilyName, marker[1], $"Unexpected Package.Current.Id.FamilyName in '{markerPath}'.");
        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageLayoutPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(marker[2])),
            ignoreCase: true,
            $"The activated app did not run from the tooling-generated loose package layout. Marker: '{markerPath}'.");
    }

    private static void AssertExecutedTestsMarker(string markerPath)
    {
        Assert.IsTrue(
            File.Exists(markerPath),
            $"The activated test host did not report its executed tests to '{markerPath}'.");
        string[] actual = File.ReadAllLines(markerPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        string[] expected =
        [
            "PlainTestMethod_Runs",
            "PlainTestMethod_HasExpectedPackageIdentity",
            "UITestMethod_RunsOnWinUIDispatcher",
        ];
        Array.Sort(expected, StringComparer.Ordinal);
        Array.Sort(actual, StringComparer.Ordinal);
        Assert.AreSequenceEqual(
            expected,
            actual,
            $"The AUMID-activated test host did not run exactly the expected plain, identity, and UI tests. Marker: '{markerPath}'.");
    }

    private static async Task AssertPackageIsRegisteredAsync(
        string packageIdentityName,
        string expectedPackageFamilyName,
        string packageLayoutPath)
    {
        string escapedIdentity = EscapePowerShellLiteral(packageIdentityName);
        string script =
            $"$packages = @(Get-AppxPackage -Name '{escapedIdentity}' -ErrorAction Stop | " +
            $"Where-Object {{ $_.Name -ceq '{escapedIdentity}' }}); " +
            "if ($packages.Count -ne 1) { throw \"Expected exactly one matching package registration, found $($packages.Count).\" }; " +
            "$package = $packages[0]; " +
            "Write-Output ('PACKAGED_WINUI_REGISTRATION=' + $package.Name + '|' + $package.PackageFamilyName + '|' + $package.InstallLocation)";
        BoundedCommandLineResult result = await RunPowerShellAsync(script);
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"Failed to verify the packaged WinUI registration for '{packageIdentityName}'." +
            $"{Environment.NewLine}Standard output:{Environment.NewLine}{result.StandardOutput}" +
            $"{Environment.NewLine}Standard error:{Environment.NewLine}{result.ErrorOutput}");

        string registrationLine = result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("PACKAGED_WINUI_REGISTRATION=", StringComparison.Ordinal));
        string[] registration = registrationLine["PACKAGED_WINUI_REGISTRATION=".Length..].Split('|');
        Assert.HasCount(3, registration, registrationLine);
        Assert.AreEqual(packageIdentityName, registration[0], registrationLine);
        Assert.AreEqual(expectedPackageFamilyName, registration[1], registrationLine);
        Assert.AreEqual(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageLayoutPath)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(registration[2])),
            ignoreCase: true,
            registrationLine);
    }

    private static async Task RemoveAndVerifyPackageRegistrationAsync(string packageIdentityName)
    {
        string escapedIdentity = EscapePowerShellLiteral(packageIdentityName);
        string script =
            $"$packages = @(Get-AppxPackage -Name '{escapedIdentity}' -ErrorAction Stop | " +
            $"Where-Object {{ $_.Name -ceq '{escapedIdentity}' }}); " +
            "foreach ($package in $packages) { Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop }; " +
            $"$remaining = @(Get-AppxPackage -Name '{escapedIdentity}' -ErrorAction Stop | " +
            $"Where-Object {{ $_.Name -ceq '{escapedIdentity}' }}); " +
            "if ($remaining.Count -ne 0) { throw \"Package registration still exists after cleanup: $($remaining.PackageFullName -join ', ')\" }; " +
            "Write-Output 'PACKAGED_WINUI_REGISTRATION_ABSENT'";
        BoundedCommandLineResult result = await RunPowerShellAsync(script);
        Assert.AreEqual(
            0,
            result.ExitCode,
            $"Failed to remove or verify removal of packaged WinUI identity '{packageIdentityName}'." +
            $"{Environment.NewLine}Standard output:{Environment.NewLine}{result.StandardOutput}" +
            $"{Environment.NewLine}Standard error:{Environment.NewLine}{result.ErrorOutput}");
        Assert.Contains(
            "PACKAGED_WINUI_REGISTRATION_ABSENT",
            result.StandardOutput,
            $"Cleanup did not verify that package identity '{packageIdentityName}' is absent.");
    }

    private static Task<BoundedCommandLineResult> RunPowerShellAsync(string script)
    {
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return RunWindowsApplicationModelCommandAsync(
            $"powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
            workingDirectory: null,
            cancellationToken: CancellationToken.None);
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string ComputePackageFamilyName(string packageIdentityName, string publisher)
    {
        const string PublisherHashAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        var publisherId = new StringBuilder(13);
        int buffer = 0;
        int bitsInBuffer = 0;
        for (int index = 0; index < 8; index++)
        {
            buffer = (buffer << 8) | hash[index];
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                publisherId.Append(PublisherHashAlphabet[(buffer >> bitsInBuffer) & 0x1F]);
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        if (bitsInBuffer > 0)
        {
            publisherId.Append(PublisherHashAlphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return $"{packageIdentityName}_{publisherId}";
    }

    private static void PatchMarkerPaths(
        string targetAssetPath,
        string identityMarkerPath,
        string executionMarkerPath)
    {
        string unitTestsPath = Path.Combine(targetAssetPath, "UnitTests.cs");
        string unitTests = File.ReadAllText(unitTestsPath)
            .PatchCodeWithReplace("$IdentityMarkerPath$", identityMarkerPath)
            .PatchCodeWithReplace("$ExecutionMarkerPath$", executionMarkerPath);
        File.WriteAllText(unitTestsPath, unitTests, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static void CopyPackagedWinUISampleAssets(string targetAssetPath)
    {
        string sourceAssets = Path.Combine(
            Constants.Root,
            "samples",
            "public",
            "mstest-runner",
            "MSTestRunnerWinUI",
            "MSTestRunnerWinUI",
            "Assets");
        Assert.IsTrue(
            Directory.Exists(sourceAssets),
            $"The canonical packaged MSTestRunnerWinUI assets were not found at '{sourceAssets}'.");

        string destinationAssets = Path.Combine(targetAssetPath, "Assets");
        Directory.CreateDirectory(destinationAssets);
        foreach (string sourceFile in Directory.GetFiles(sourceAssets, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourceFile, Path.Combine(destinationAssets, Path.GetFileName(sourceFile)), overwrite: true);
        }
    }

    private static async Task EnsureGeneratedCSharpFilesHaveUtf8BomAsync(
        string targetAssetPath,
        CancellationToken cancellationToken)
    {
        byte[] expectedPreamble = Encoding.UTF8.GetPreamble();
        foreach (string path in Directory.GetFiles(targetAssetPath, "*.cs", SearchOption.AllDirectories))
        {
            string content = await File.ReadAllTextAsync(path, cancellationToken);
            await File.WriteAllTextAsync(
                path,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                cancellationToken);

            byte[] actualPreamble = new byte[expectedPreamble.Length];
            await using FileStream stream = File.OpenRead(path);
            int bytesRead = await stream.ReadAsync(actualPreamble, cancellationToken);
            Assert.AreEqual(expectedPreamble.Length, bytesRead, $"Generated C# file '{path}' is shorter than the UTF-8 BOM.");
            Assert.AreSequenceEqual(expectedPreamble, actualPreamble, $"Generated C# file '{path}' is not UTF-8 with BOM.");
        }
    }

    private sealed record PackagedWinUIBuild(
        string PackageLayoutPath,
        string GeneratedManifestPath,
        string ResolvedAssetsReportPath,
        string BinlogPath);

    private sealed record GeneratedPackagedWinUIAsset(
        string AssetName,
        TestAsset TestAsset,
        string IdentityMarkerPath,
        string PackageIdentityName,
        string ExpectedPackageFamilyName,
        string ExecutionMarkerPath);

    private const string SourceCode = """
#file $AssetName$.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <RootNamespace>$AssetName$</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>$RuntimeIdentifier$</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <WinUIDSKReferences>false</WinUIDSKReferences>
    <EnableMsixTooling>true</EnableMsixTooling>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
    <Nullable>enable</Nullable>
    <NoWarn>$(NoWarn);NETSDK1201;TPEXP</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <Page Remove="UnitTestApp.xaml" />
    <ApplicationDefinition Include="UnitTestApp.xaml" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Assets\**\*" />
    <Manifest Include="$(ApplicationManifest)" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$WindowsSdkBuildToolsVersion$" />
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="$WindowsAppSdkVersion$" />
  </ItemGroup>

  <Target Name="WriteResolvedWinUIAssets"
          BeforeTargets="CoreCompile"
          DependsOnTargets="_CalculateGenerateTestingPlatformEntryPoint">
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="UseWinUI=$(UseWinUI);TargetFramework=$(TargetFramework);RuntimeIdentifier=$(RuntimeIdentifier);UsingMSTestSdk=$(UsingMSTestSdk);EnableMSTestRunner=$(EnableMSTestRunner);GenerateTestingPlatformEntryPoint=$(GenerateTestingPlatformEntryPoint);GenerateTestingPlatformApplicationHelper=$(GenerateTestingPlatformApplicationHelper);EnableMicrosoftTestingExtensionsPackagedApp=$(EnableMicrosoftTestingExtensionsPackagedApp)"
      Overwrite="true" />
    <WriteLinesToFile
      File="$(MSBuildProjectDirectory)\resolved-mstest-assets.txt"
      Lines="@(ReferencePath->'%(FullPath)')"
      Overwrite="false" />
  </Target>
</Project>

#file app.manifest
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="$AssetName$.app" />
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>

#file Package.appxmanifest
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="$PackageIdentityName$" Publisher="$Publisher$" Version="1.0.0.0" />
  <mp:PhoneIdentity PhoneProductId="$PhoneProductId$" PhonePublisherId="00000000-0000-0000-0000-000000000000" />
  <Properties>
    <DisplayName>$AssetName$</DisplayName>
    <PublisherDisplayName>MSTest acceptance</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.17763.0" MaxVersionTested="10.0.19041.0" />
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.19041.0" />
  </Dependencies>
  <Resources>
    <Resource Language="x-generate" />
  </Resources>
  <Applications>
    <Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$">
      <uap:VisualElements
        DisplayName="$AssetName$"
        Description="MSTest packaged WinUI acceptance asset"
        BackgroundColor="transparent"
        Square150x150Logo="Assets\Square150x150Logo.png"
        Square44x44Logo="Assets\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Assets\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Assets\SplashScreen.png" />
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>

#file UnitTestApp.xaml
<?xml version="1.0" encoding="utf-8"?>
<Application
    x:Class="$AssetName$.UnitTestApp"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:$AssetName$">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>

#file UnitTestApp.xaml.cs
using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace $AssetName$;

public partial class UnitTestApp : Application
{
    private Window? _window;

    public UnitTestApp() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new UnitTestAppWindow();
        _window.Activate();
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

        try
        {
            Environment.ExitCode = await MicrosoftTestingPlatformApplication.RunAsync(
                Environment.GetCommandLineArgs().Skip(1).ToArray());
        }
        finally
        {
            _window.Close();
            Exit();
        }
    }
}

#file UnitTestAppWindow.xaml
<?xml version="1.0" encoding="utf-8"?>
<Window
    x:Class="$AssetName$.UnitTestAppWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:$AssetName$"
    Title="$AssetName$">
  <Grid />
</Window>

#file UnitTestAppWindow.xaml.cs
using Microsoft.UI.Xaml;

namespace $AssetName$;

public sealed partial class UnitTestAppWindow : Window
{
    public UnitTestAppWindow() => InitializeComponent();
}

#file UnitTests.cs
using System;
using System.IO;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.ApplicationModel;

namespace $AssetName$;

[TestClass]
public sealed class PackagedWinUITestCases
{
    private static readonly object s_executionMarkerLock = new();

    [TestMethod]
    public void PlainTestMethod_Runs()
    {
        RecordExecution(nameof(PlainTestMethod_Runs));
    }

    [TestMethod]
    public void PlainTestMethod_HasExpectedPackageIdentity()
    {
        Package package = Package.Current;
        Assert.AreEqual("$PackageIdentityName$", package.Id.Name);
        Assert.AreEqual("$ExpectedPackageFamilyName$", package.Id.FamilyName);
        File.WriteAllText(
            @"$IdentityMarkerPath$",
            $"{package.Id.Name}|{package.Id.FamilyName}|{AppContext.BaseDirectory}");
        RecordExecution(nameof(PlainTestMethod_HasExpectedPackageIdentity));
    }

    [UITestMethod]
    public void UITestMethod_RunsOnWinUIDispatcher()
    {
        var grid = new Grid();

        Assert.IsTrue(grid.DispatcherQueue.HasThreadAccess);
        RecordExecution(nameof(UITestMethod_RunsOnWinUIDispatcher));
    }

    private static void RecordExecution(string testName)
    {
        lock (s_executionMarkerLock)
        {
            File.AppendAllLines(@"$ExecutionMarkerPath$", [testName]);
        }
    }
}
""";
}
