// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

public abstract class AcceptanceTestBase<TFixture>
    where TFixture : ITestAssetFixture, new()
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

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}
""";

    static AcceptanceTestBase()
    {
        var cpmPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        MicrosoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;

        MSTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.");
        MicrosoftTestingPlatformVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Platform.");
        MSTestEngineVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.Engine.");
    }

    protected static TFixture AssetFixture { get; private set; } = default!;

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

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fine in this context")]
    public static async Task ClassInitialize(TestContext testContext)
    {
        AssetFixture = new();
        await AssetFixture.InitializeAsync(testContext.CancellationToken);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Fine in this context")]
    public static void ClassCleanup()
        => AssetFixture.Dispose();

    internal static IEnumerable<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)> GetBuildMatrixTfmBuildVerbConfiguration()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                foreach (Verb verb in Enum.GetValues<Verb>())
                {
                    yield return new(tfm, compilationMode, verb);
                }
            }
        }
    }

    internal static IEnumerable<(string Tfm, BuildConfiguration BuildConfiguration)> GetBuildMatrixTfmBuildConfiguration()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
            {
                yield return new(tfm, compilationMode);
            }
        }
    }

    internal static IEnumerable<(string MultiTfm, BuildConfiguration BuildConfiguration)> GetBuildMatrixMultiTfmFoldedBuildConfiguration()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
        {
            yield return new(TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode);
        }
    }

    internal static IEnumerable<(string MultiTfm, BuildConfiguration BuildConfiguration)> GetBuildMatrixMultiTfmBuildConfiguration()
    {
        foreach (BuildConfiguration compilationMode in Enum.GetValues<BuildConfiguration>())
        {
            yield return new(TargetFrameworks.All.ToMSBuildTargetFrameworks(), compilationMode);
        }
    }

    internal static IEnumerable<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)> GetBuildMatrixSingleAndMultiTfmBuildConfiguration()
    {
        foreach ((string Tfm, BuildConfiguration BuildConfiguration) entry in GetBuildMatrixTfmBuildConfiguration())
        {
            yield return new(entry.Tfm, entry.BuildConfiguration, false);
        }

        foreach ((string MultiTfm, BuildConfiguration BuildConfiguration) entry in GetBuildMatrixMultiTfmBuildConfiguration())
        {
            yield return new(entry.MultiTfm, entry.BuildConfiguration, true);
        }
    }

    // https://github.com/NuGet/NuGet.Client/blob/c5934bdcbc578eec1e2921f49e6a5d53481c5099/test/NuGet.Core.FuncTests/Msbuild.Integration.Test/MsbuildIntegrationTestFixture.cs#L65-L94
    private protected static async Task<string> FindMsbuildWithVsWhereAsync(CancellationToken cancellationToken)
    {
        string vswherePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
        string path = await RunAndGetSingleLineStandardOutputAsync(vswherePath, "-find MSBuild\\**\\Bin\\MSBuild.exe", cancellationToken);
        return path;
    }

    private static async Task<string> RunAndGetSingleLineStandardOutputAsync(string vswherePath, string arg, CancellationToken cancellationToken)
    {
        var commandLine = new TestInfrastructure.CommandLine();
        await commandLine.RunAsync($"\"{vswherePath}\" -latest -prerelease -requires Microsoft.Component.MSBuild {arg}", cancellationToken: cancellationToken);

        string? path = null;
        using (var stringReader = new StringReader(commandLine.StandardOutput))
        {
            string? line;
            while ((line = await stringReader.ReadLineAsync(cancellationToken)) != null)
            {
                if (path != null)
                {
                    throw new Exception("vswhere returned more than 1 line");
                }

                path = line;
            }
        }

        return path!;
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
            matches = [.. matches
                // (full path, file name without prefix)
                .Select(path => (path, fileName: Path.GetFileName(path)[packagePrefixName.Length..]))
                // check if first character of file name without prefix is number
                .Where(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _))
                // take the full path
                .Select(tuple => tuple.path)];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Was expecting to find a single NuGet package named '{packagePrefixName}' in '{rootFolder}', but found {matches.Length}: '{string.Join("', '", matches.Select(m => Path.GetFileName(m)))}'.");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(packagePrefixName.Length, packageFullName.Length - packagePrefixName.Length - NuGetPackageExtensionName.Length);
    }
}
