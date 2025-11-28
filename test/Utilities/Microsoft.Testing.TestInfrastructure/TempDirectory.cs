// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.TestInfrastructure;

public class TempDirectory : IDisposable
{
    private readonly bool _cleanup;
    private readonly string _baseDirectory;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TempDirectory"/> class.
    /// </summary>
    public TempDirectory(string? subDirectory = null)
    {
        _cleanup = Environment.GetEnvironmentVariable("Microsoft_Testing_TestInfrastructure_TempDirectory_Cleanup") != "0";
        (_baseDirectory, Path) = CreateUniqueDirectory(subDirectory);
    }

    public string Path { get; }

#pragma warning disable CS0618 // Type or member is obsolete - This is the only place where GetRepoRoot and GetTestSuiteDirectory should be called.
    internal static string RepoRoot { get; } = GetRepoRoot();

    public static string TestSuiteDirectory { get; } = GetTestSuiteDirectory();
#pragma warning restore CS0618 // Type or member is obsolete

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (!_cleanup)
        {
            return;
        }

        if (!Directory.Exists(_baseDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_baseDirectory, recursive: true);
        }
        catch
        {
        }
    }

    public DirectoryInfo CreateDirectory(string dir) => Directory.CreateDirectory(System.IO.Path.Combine(Path, dir));

    public static async Task WriteFileAsync(string targetDirectory, string fileName, string fileContents)
    {
        string finalFile = System.IO.Path.Combine(targetDirectory, fileName);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(finalFile)!);
        using FileStream fs = new(finalFile, FileMode.CreateNew);
        using StreamWriter stream = new(fs);
        await stream.WriteLineAsync(fileContents);
    }

    public async Task CopyDirectoryAsync(string sourceDirectory, string targetDirectory, bool retainAttributes = false) => await CopyDirectoryAsync(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory), retainAttributes);

    public static async Task CopyDirectoryAsync(DirectoryInfo source, DirectoryInfo target, bool retainAttributes = false)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
        {
            if (retainAttributes)
            {
                File.Copy(fi.FullName, System.IO.Path.Combine(target.FullName, fi.Name));
            }
            else
            {
                using FileStream fileStream = File.OpenRead(fi.FullName);
                using FileStream destinationStream = new(
                    System.IO.Path.Combine(target.FullName, fi.Name),
                    FileMode.CreateNew);
                await fileStream.CopyToAsync(destinationStream);
            }
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            await CopyDirectoryAsync(diSourceSubDir, nextTargetSubDir);
        }
    }

    public void CopyDirectory(string sourceDirectory, string targetDirectory) => CopyDirectory(new DirectoryInfo(sourceDirectory), new DirectoryInfo(targetDirectory));

    public static void CopyDirectory(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in source.GetFiles())
        {
            fi.CopyTo(System.IO.Path.Combine(target.FullName, fi.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);
            CopyDirectory(diSourceSubDir, nextTargetSubDir);
        }
    }

    /// <summary>
    /// Copy given files into the TempDirectory and return the updated paths that are pointing to TempDirectory.
    /// </summary>
    public string[] CopyFile(params string[] filePaths)
    {
        List<string> paths = new(filePaths.Length);
        foreach (string filePath in filePaths)
        {
            string destination = System.IO.Path.Combine(Path, System.IO.Path.GetFileName(filePath));
            File.Copy(filePath, destination);
            paths.Add(destination);
        }

        return [.. paths];
    }

    /// <summary>
    /// Copy given file into TempDirectory and return the updated path.
    /// </summary>
    public string CopyFile(string filePath)
    {
        string destination = System.IO.Path.Combine(Path, System.IO.Path.GetFileName(filePath));
        File.Copy(filePath, destination);
        return destination;
    }

    [Obsolete("Don't use directly. Use TestSuiteDirectory property instead.")]
    private static string GetTestSuiteDirectory()
        => System.IO.Path.Combine(RepoRoot, "artifacts", "tmp", Constants.BuildConfiguration, "testsuite");

    [Obsolete("Don't use directly. Use RepoRoot property instead.")]
    private static string GetRepoRoot()
    {
        string? currentDirectory = AppContext.BaseDirectory;
        while (System.IO.Path.GetFileName(currentDirectory) != "artifacts" && currentDirectory is not null)
        {
            currentDirectory = System.IO.Path.GetDirectoryName(currentDirectory);
        }

        return System.IO.Path.GetDirectoryName(currentDirectory)
            ?? throw new InvalidOperationException("artifacts folder not found");
    }

    /// <summary>
    /// Creates an unique temporary directory.
    /// </summary>
    /// <returns>
    /// Path of the created directory.
    /// </returns>
    internal static (string BaseDirectory, string FinalDirectory) CreateUniqueDirectory(string? subDirectory)
    {
        string directoryPath = System.IO.Path.Combine(TestSuiteDirectory, RandomId.Next());
        Directory.CreateDirectory(directoryPath);

        string directoryBuildProps = System.IO.Path.Combine(directoryPath, "Directory.Build.props");
        File.WriteAllText(directoryBuildProps, $"""
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
      <RepoRoot>{RepoRoot}/</RepoRoot>
      <!-- Do not warn about package downgrade. NuGet uses alphabetical sort as ordering so -dev or -ci are considered downgrades of -preview. -->
      <NoWarn>NU1605</NoWarn>
      <RunAnalyzers>false</RunAnalyzers>
      <!-- Prevent build warnings/errors on unsupported TFMs -->
      <CheckEolTargetFramework>false</CheckEolTargetFramework>
    </PropertyGroup>
</Project>
""");

        string directoryBuildTarget = System.IO.Path.Combine(directoryPath, "Directory.Build.targets");
        File.WriteAllText(directoryBuildTarget, $"""
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <ItemGroup>
        <!-- EnableMicrosoftTestingPlatform is already handled by MSTest.Sdk, but not when using MSTest metapackage -->
        <!-- Historically, EnableMicrosoftTestingPlatform existed first in MSTest.Sdk with the goal of fixing our tests -->
        <!-- Then, this code was introduced. -->
        <!-- As EnableMicrosoftTestingPlatform isn't expected/intended to be used by users, it may be possible to remove it from MSTest.Sdk -->
        <!-- The code here should solve the issue either way. -->
        <!--
            This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
            we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
            ensure we are testing with locally built version, we force adding the platform dependency.
        -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="{AppVersion.DefaultSemVer}" Condition="'$(UsingMSTestSdk)' != 'true' AND '$(EnableMicrosoftTestingPlatform)' == 'true'" />
    </ItemGroup>

    <Target Name="WorkaroundMacOSDumpIssue" AfterTargets="Build" Condition="$([MSBuild]::IsOSPlatform('OSX')) AND '$(UseAppHost)' != 'false' AND '$(TargetFramework)' != ''">
        <Exec Command="codesign --sign - --force --entitlements '$(MSBuildThisFileDirectory)mtp-test-entitlements.plist' '$(RunCommand)'" />
    </Target>
</Project>
""");

        File.WriteAllText(System.IO.Path.Combine(directoryPath, "mtp-test-entitlements.plist"), """
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
    <dict>
    <key>com.apple.security.cs.allow-jit</key>
        <true/>
    <key>com.apple.security.cs.allow-dyld-environment-variables</key>
        <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
        <true/>
    <key>com.apple.security.cs.debugger</key>
        <true/>
    <key>com.apple.security.get-task-allow</key>
        <true/>
    </dict>
</plist>
""");

        string directoryPackagesProps = System.IO.Path.Combine(directoryPath, "Directory.Packages.props");
        File.WriteAllText(directoryPackagesProps, """
<?xml version="1.0" encoding="utf-8"?>
<Project>
    <PropertyGroup>
      <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
    </PropertyGroup>
</Project>
""");

        string finalDirectory = directoryPath;
        if (!string.IsNullOrWhiteSpace(subDirectory))
        {
            finalDirectory = System.IO.Path.Combine(directoryPath, subDirectory);
        }

        Directory.CreateDirectory(finalDirectory);

        return (directoryPath, finalDirectory);
    }

    public override string ToString() => Path;
}
