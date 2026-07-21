// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace Microsoft.MSTestV2.CLIAutomation;

/// <summary>
/// Helper class to run the packaged VSTest console.
/// </summary>
public static class VSTestConsoleLocator
{
    private const string TestPlatformPackageName = "Microsoft.TestPlatform";
    private const string TestPlatformCliPackageName = "Microsoft.TestPlatform.CLI";

    /// <summary>
    /// Runs the packaged VSTest console.
    /// </summary>
    public static async Task<VSTestConsoleResult> RunAsync(
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        string testPlatformVersion = GetTestPlatformVersion();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string vstestConsolePath = GetWindowsConsoleRunnerPath(testPlatformVersion);
            using var commandLine = new CommandLine();
            int exitCode = await commandLine.RunAsyncAndReturnExitCodeAsync(
                $"\"{vstestConsolePath}\" {arguments}",
                workingDirectory: workingDirectory,
                cancellationToken: cancellationToken);

            return new(exitCode, commandLine.StandardOutput, commandLine.ErrorOutput);
        }

        string vstestConsoleDllPath = GetManagedConsoleRunnerPath(testPlatformVersion);
        using var dotnetMuxer = new DotnetMuxer();
        int managedExitCode = await dotnetMuxer.ExecuteAsync(
            $"\"{vstestConsoleDllPath}\" {arguments}",
            workingDirectory,
            cancellationToken);

        return new(managedExitCode, dotnetMuxer.StandardOutput, dotnetMuxer.StandardError);
    }

    private static string GetWindowsConsoleRunnerPath(string testPlatformVersion)
    {
        string testPlatformNuGetPackageFolder = GetNugetPackageFolder(TestPlatformPackageName, testPlatformVersion);
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
            ? throw GetExceptionForVSTestConsoleNotFound()
            : vstestConsolePath;

        InvalidOperationException GetExceptionForVSTestConsoleNotFound()
        {
            string[] files = Directory.GetFiles(testPlatformNuGetPackageFolder, "vstest.console.exe", SearchOption.AllDirectories);
            return files.Length == 0
                ? new InvalidOperationException($"Could not find vstest.console.exe in {vstestConsolePath}")
                : new InvalidOperationException($"Could not find vstest.console.exe in {vstestConsolePath}. Found in:{Environment.NewLine}{string.Join(Environment.NewLine, files)}");
        }
    }

    private static string GetManagedConsoleRunnerPath(string testPlatformVersion)
    {
        string testPlatformCliNuGetPackageFolder = GetNugetPackageFolder(TestPlatformCliPackageName, testPlatformVersion);
        string[] files = Directory.GetFiles(testPlatformCliNuGetPackageFolder, "vstest.console.dll", SearchOption.AllDirectories);

        return files.Length == 1
            ? files[0]
            : throw new InvalidOperationException(
                $"Expected one vstest.console.dll in {testPlatformCliNuGetPackageFolder}, but found:{Environment.NewLine}{string.Join(Environment.NewLine, files)}");
    }

    private static string GetNugetPackageFolder(string packageName, string packageVersion)
    {
        string packageDirectoryName = packageName.ToLowerInvariant();
        string? nugetPackagesFolderPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackagesFolderPath))
        {
            Assert.IsTrue(Directory.Exists(nugetPackagesFolderPath), $"Found environment variable 'NUGET_PACKAGES' and NuGet package folder '{nugetPackagesFolderPath}' should exist");
            string fullPackagePath = Path.Combine(nugetPackagesFolderPath, packageDirectoryName, packageVersion);
            if (Directory.Exists(fullPackagePath))
            {
                return fullPackagePath;
            }
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        nugetPackagesFolderPath = Path.Combine(userProfile, ".nuget", "packages");
        Assert.IsTrue(Directory.Exists(nugetPackagesFolderPath), $"NuGet package folder '{nugetPackagesFolderPath}' should exist");

        string fullPackagePathFromUserProfile = Path.Combine(nugetPackagesFolderPath, packageDirectoryName, packageVersion);
        Assert.IsTrue(Directory.Exists(fullPackagePathFromUserProfile), $"NuGet package folder '{fullPackagePathFromUserProfile}' should exist");
        return fullPackagePathFromUserProfile;
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

public sealed class VSTestConsoleResult(int exitCode, string standardOutput, string standardError)
{
    public int ExitCode { get; } = exitCode;

    public string StandardOutput { get; } = standardOutput;

    public string StandardError { get; } = standardError;
}
