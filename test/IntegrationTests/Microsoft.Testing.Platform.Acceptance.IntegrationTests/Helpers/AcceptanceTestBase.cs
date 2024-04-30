﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
    private const string MicrosoftTestingPlatformNamePrefix = "Microsoft.Testing.Platform.";
    private const string NuGetPackageExtensionName = ".nupkg";
#if !MSTEST_DOWNLOADED
    private const string MSTestTestFrameworkPackageNamePrefix = "MSTest.TestFramework.";
#endif

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
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$MicrosoftNETTestSdkVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
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

        var versionsPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "eng", "Versions.props"));
#if MSTEST_DOWNLOADED
        MSTestVersion = ExtractVersionFromVersionPropsFile(versionsPropFileDoc, "MSTestVersion");
        MicrosoftTestingPlatformVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, MicrosoftTestingPlatformNamePrefix);
        MicrosoftTestingPlatformExtensionsVersion = MicrosoftTestingPlatformVersion;
#else
        MSTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, MSTestTestFrameworkPackageNamePrefix);
        MicrosoftTestingPlatformVersion = ExtractVersionFromPackage(Constants.ArtifactsTmpPackages, MicrosoftTestingPlatformNamePrefix);
        MicrosoftTestingPlatformExtensionsVersion = ExtractVersionFromVersionPropsFile(versionsPropFileDoc, "MicrosoftTestingPlatformVersion");
#endif
    }

    protected AcceptanceTestBase(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    internal static string RID { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    public static string MSTestVersion { get; private set; }

    public static string MicrosoftNETTestSdkVersion { get; private set; }

    public static string MicrosoftTestingPlatformVersion { get; private set; }

    public static string MicrosoftTestingPlatformExtensionsVersion { get; private set; }

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
            // So we need to find the first package that contains a number after the prefix.
            // Ideally, we would want to do a full validation to check this is a nuget version number, but that's too much work for now.
            matches =
            [
                matches.Select(path => (path, fileName: Path.GetFileName(path)[packagePrefixName.Length..]))
                    .First(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _)).path
            ];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Was expecting to find a single NuGet package named '{packagePrefixName}' in '{rootFolder}' but found {matches.Length}.");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(packagePrefixName.Length, packageFullName.Length - packagePrefixName.Length - NuGetPackageExtensionName.Length);
    }

    private static string ExtractVersionFromVersionPropsFile(XDocument versionPropsXmlDocument, string entryName)
    {
        XElement[] matches = versionPropsXmlDocument.Descendants(entryName).ToArray();
        return matches.Length != 1
            ? throw new InvalidOperationException($"Was expecting to find a single entry for '{entryName}' but found {matches.Length}.")
            : matches[0].Value;
    }
}
