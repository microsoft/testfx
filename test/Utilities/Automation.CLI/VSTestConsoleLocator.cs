// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Microsoft.MSTestV2.CLIAutomation;

/// <summary>
/// Helper class to locate vstest.console.exe.
/// </summary>
public static class VSTestConsoleLocator
{
    private const string TestPlatformPackageName = "Microsoft.TestPlatform";

    /// <summary>
    /// Gets the path to <c>vstest.console.exe</c>.
    /// </summary>
    /// <returns>Full path to <c>vstest.console.exe</c>.</returns>
    public static string GetConsoleRunnerPath()
    {
        string testPlatformNuGetPackageFolder = Path.Combine(
            GetNugetPackageFolder(),
            TestPlatformPackageName,
            GetTestPlatformVersion());
        if (!Directory.Exists(testPlatformNuGetPackageFolder))
        {
            throw new DirectoryNotFoundException($"Test platform NuGet package folder '{testPlatformNuGetPackageFolder}' does not exist");
        }

        string vstestConsolePath = Path.Combine(
            testPlatformNuGetPackageFolder,
            "tools",
            "net462",
            "Common7",
            "IDE",
            "Extensions",
            "TestPlatform",
            "vstest.console.exe");
        return !File.Exists(vstestConsolePath)
            ? throw GetExceptionForVSTestConsoleNotFound(vstestConsolePath)
            : vstestConsolePath;

        InvalidOperationException GetExceptionForVSTestConsoleNotFound(string expectedPath)
        {
            string[] files = Directory.GetFiles(testPlatformNuGetPackageFolder, "vstest.console.exe", SearchOption.AllDirectories);
            return files.Length == 0
                ? new InvalidOperationException($"Could not find vstest.console.exe in {vstestConsolePath}")
                : new InvalidOperationException($"Could not find vstest.console.exe in {vstestConsolePath}. Found in:{Environment.NewLine}{string.Join(Environment.NewLine, files)}");
        }
    }

    private static string GetNugetPackageFolder()
    {
        string? nugetPackagesFolderPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackagesFolderPath))
        {
            Assert.IsTrue(Directory.Exists(nugetPackagesFolderPath), $"Found environment variable 'NUGET_PACKAGES' and NuGet package folder '{nugetPackagesFolderPath}' should exist");

            return nugetPackagesFolderPath;
        }

        string? userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(userProfile))
        {
            throw new InvalidOperationException("USERPROFILE environment variable is not set");
        }

        nugetPackagesFolderPath = Path.Combine(userProfile, ".nuget", "packages");
        Assert.IsTrue(Directory.Exists(nugetPackagesFolderPath), $"NuGet package folder '{nugetPackagesFolderPath}' should exist");

        return nugetPackagesFolderPath;
    }

    private static string GetTestPlatformVersion()
    {
        string cpmFilePath = Path.Combine(RootFinder.Find(), "Directory.Packages.props");
        using FileStream fileStream = File.OpenRead(cpmFilePath);
#pragma warning disable CA3075 // Insecure DTD processing in XML
        using var xmlTextReader = new XmlTextReader(fileStream) { Namespaces = false };
#pragma warning restore CA3075 // Insecure DTD processing in XML
        var cpmXml = new XmlDocument();
        cpmXml.Load(xmlTextReader);

        return cpmXml.DocumentElement?.SelectSingleNode("PropertyGroup/MicrosoftNETTestSdkVersion")?.InnerText
            ?? throw new InvalidOperationException($"Could not find MicrosoftNETTestSdkVersion in {cpmFilePath}");
    }
}
