// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// All the properties of this class should be non static.
/// At the moment are static because we need to share them between perclass/id fixtures and
/// it's not supported at the moment.
/// </summary>
public abstract class AcceptanceTestBase : TestBase
{
    private const string NuGetPackageExtensionName = ".nupkg";

    protected const string CurrentMSTestSourceCode = """
#file MSTestProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <PlatformTarget>x64</PlatformTarget>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    $TargetFramework$
    $OutputType$
    $EnableMSTestRunner$
    $Extra$
    <NoWarn>$(NoWarn);NETSDK1201</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";

    static AcceptanceTestBase()
    {
        var cpmPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        MicrosoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;

        var versionsPropsFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "eng", "Versions.props"));
        var directoryPackagesPropsFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        MSTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.");
        MicrosoftTestingPlatformVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Platform.");
        MicrosoftTestingEnterpriseExtensionsVersion = ExtractVersionFromXmlFile(versionsPropsFileDoc, "MicrosoftTestingExtensionsRetryVersion");
        MicrosoftTestingInternalFrameworkVersion = ExtractVersionFromXmlFile(directoryPackagesPropsFileDoc, "MicrosoftTestingInternalFrameworkVersion");
        MSTestEngineVersion = ExtractVersionFromXmlFile(versionsPropsFileDoc, "MSTestEngineVersion");
    }

    protected AcceptanceTestBase(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    internal static string RID { get; }
        = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win-x64"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "linux-x64"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "osx-x64"
                    : throw new NotSupportedException("Current OS is not supported");

    public static string MSTestVersion { get; private set; }

    public static string MSTestEngineVersion { get; private set; }

    public static string MicrosoftNETTestSdkVersion { get; private set; }

    public static string MicrosoftTestingPlatformVersion { get; private set; }

    public static string MicrosoftTestingEnterpriseExtensionsVersion { get; private set; }

    public static string MicrosoftTestingInternalFrameworkVersion { get; private set; }

    internal static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)>> GetBuildMatrixTfmBuildVerbConfiguration()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                foreach (Verb verb in Enum.GetValues<Verb>())
                {
                    yield return new TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)>((tfm.Arguments, compilationMode, verb), $"{tfm.Arguments},{compilationMode},{verb}");
                }
            }
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)>> GetBuildMatrixTfmBuildConfiguration()
    {
        foreach (TestArgumentsEntry<string> tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                yield return new TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)>((tfm.Arguments, compilationMode), $"{tfm.Arguments},{compilationMode}");
            }
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)>> GetBuildMatrixMultiTfmFoldedBuildConfiguration()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
        {
            yield return new TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)>((TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode), $"multitfm,{compilationMode}");
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)>> GetBuildMatrixMultiTfmBuildConfiguration()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
        {
            yield return new TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)>((TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode), $"{TargetFrameworks.All.ToMSBuildTargetFrameworks()},{compilationMode}");
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)>> GetBuildMatrixSingleAndMultiTfmBuildConfiguration()
    {
        foreach (TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)> entry in GetBuildMatrixTfmBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)>(
                (entry.Arguments.Tfm, entry.Arguments.BuildConfiguration, false), $"{entry.Arguments.Tfm},{entry.Arguments.BuildConfiguration}");
        }

        foreach (TestArgumentsEntry<(string MultiTfm, BuildConfiguration BuildConfiguration)> entry in GetBuildMatrixMultiTfmBuildConfiguration())
        {
            yield return new TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)>(
                (entry.Arguments.MultiTfm, entry.Arguments.BuildConfiguration, true), $"multitfm,{entry.Arguments.BuildConfiguration}");
        }
    }

    private static string ExtractVersionFromPackage(string rootFolder, string packagePrefixName)
    {
        string[] matches = Directory.GetFiles(rootFolder, packagePrefixName + "*" + NuGetPackageExtensionName, SearchOption.TopDirectoryOnly);

        if (matches.Length > 1)
        {
            // For some packages the find pattern will match multiple packages, for example:
            // Microsoft.Testing.Platform.1.0.0.nupkg
            // Microsoft.Testing.Platform.Extensions.1.0.0.nupkg
            // So we need to find a package that contains a number after the prefix.
            // Ideally, we would want to do a full validation to check this is a nuget version number, but that's too much work for now.
            matches = matches
                    // (full path, file name without prefix)
                    .Select(path => (path, fileName: Path.GetFileName(path)[packagePrefixName.Length..]))
                    // check if first character of file name without prefix is number
                    .Where(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _))
                    // take the full path
                    .Select(tuple => tuple.path)
                    .ToArray();
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Was expecting to find a single NuGet package named '{packagePrefixName}' in '{rootFolder}', but found {matches.Length}: '{string.Join("', '", matches.Select(m => Path.GetFileName(m)))}'.");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(packagePrefixName.Length, packageFullName.Length - packagePrefixName.Length - NuGetPackageExtensionName.Length);
    }

    private static string ExtractVersionFromXmlFile(XDocument versionPropsXmlDocument, string entryName)
    {
        XElement[] matches = versionPropsXmlDocument.Descendants(entryName).ToArray();
        return matches.Length != 1
            ? throw new InvalidOperationException($"Was expecting to find a single entry for '{entryName}' but found {matches.Length}.")
            : matches[0].Value;
    }
}
