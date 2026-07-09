// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Combinatorial.MSTest;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

public abstract class AcceptanceTestBase
{
    private const string NuGetPackageExtensionName = ".nupkg";

    static AcceptanceTestBase()
    {
        var cpmPropFileDoc = XDocument.Load(Path.Combine(RootFinder.Find(), "Directory.Packages.props"));
        MicrosoftNETTestSdkVersion = cpmPropFileDoc.Descendants("MicrosoftNETTestSdkVersion").Single().Value;

        MSTestVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework.");
        MicrosoftTestingPlatformVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Platform.");
        MSTestSourceGenerationVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "MSTest.SourceGeneration.");
        MicrosoftTestingExtensionsLoggingVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.Logging.");
        MicrosoftTestingExtensionsCtrfReportVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.CtrfReport.");
        MicrosoftTestingExtensionsJUnitReportVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.JUnitReport.");
        MicrosoftTestingExtensionsGitHubActionsReportVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.GitHubActionsReport.");
        MicrosoftTestingExtensionsPackagedAppVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.PackagedApp.");
        MicrosoftTestingExtensionsVideoRecorderVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesShipping, "Microsoft.Testing.Extensions.VideoRecorder.");
        MicrosoftTestingExtensionsAzureFoundryVersion = ExtractVersionFromPackage(Constants.ArtifactsPackagesNonShipping, "Microsoft.Testing.Extensions.AzureFoundry.");
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

    public static string MSTestSourceGenerationVersion { get; private set; }

    public static string MicrosoftNETTestSdkVersion { get; private set; }

    public static string MicrosoftTestingPlatformVersion { get; private set; }

    public static string MicrosoftTestingExtensionsLoggingVersion { get; private set; }

    public static string MicrosoftTestingExtensionsCtrfReportVersion { get; private set; }

    public static string MicrosoftTestingExtensionsJUnitReportVersion { get; private set; }

    public static string MicrosoftTestingExtensionsGitHubActionsReportVersion { get; private set; }

    public static string MicrosoftTestingExtensionsPackagedAppVersion { get; private set; }

    public static string MicrosoftTestingExtensionsVideoRecorderVersion { get; private set; }

    public static string MicrosoftTestingExtensionsAzureFoundryVersion { get; private set; }

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

    /// <summary>
    /// Gets the metadata modes each acceptance assertion should run against. By default the runtime
    /// reflection path and both source-generated paths (the <c>MSTest.SourceGeneration</c> package's
    /// <c>Rooting</c> and <c>ReflectionFree</c> modes) are exercised; the source-gen modes are dropped
    /// when globally disabled via the kill-switch.
    /// </summary>
    internal static MetadataMode[] MetadataModesToRun { get; }
        = AcceptanceSourceGen.IsGloballyDisabled
            ? [MetadataMode.Reflection]
            : [MetadataMode.Reflection, MetadataMode.SourceGeneration, MetadataMode.AotSourceGeneration];

    /// <summary>
    /// DynamicData source: every <see cref="TargetFrameworks.All"/> TFM combined with every applicable
    /// <see cref="MetadataModesToRun"/> mode. Source generation is .NET-only, so .NET Framework TFMs
    /// (net4x) are paired with <see cref="MetadataMode.Reflection"/> only.
    /// <para>
    /// This cross-parameter filter (net4x → reflection only) cannot be expressed with independent
    /// <c>[CombinatorialData]</c> parameter providers, so it stays a bespoke data source. Matrices
    /// without such a dependency should instead use <c>[CombinatorialData]</c> with
    /// <see cref="MetadataModeValuesAttribute"/> (and <see cref="AllTargetFrameworksAttribute"/> when
    /// a TFM axis is needed).
    /// </para>
    /// </summary>
    public static IEnumerable<object[]> AllTfmsAndMetadataModes()
    {
        foreach (string tfm in TargetFrameworks.All)
        {
            // Source generation is .NET-only, so .NET Framework TFMs are paired with reflection only.
            foreach (MetadataMode mode in MetadataModesToRun.Where(mode => mode == MetadataMode.Reflection || TargetFrameworks.Net.Contains(tfm)))
            {
                yield return [tfm, mode];
            }
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
}

public abstract class AcceptanceTestBase<TFixture> : AcceptanceTestBase
    where TFixture : ITestAssetFixture, new()
{
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

    protected static TFixture AssetFixture { get; private set; } = default!;

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
}

/// <summary>
/// A <see cref="ICombinatorialValuesProvider"/> that yields every target framework in
/// <see cref="TargetFrameworks.All"/> (the OS-dependent set, which includes .NET Framework on Windows).
/// Apply it to a <see langword="string"/> parameter of a <c>[CombinatorialData]</c> test method to combine
/// over all target frameworks without hand-writing a bespoke build-matrix data source.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class AllTargetFrameworksAttribute : Attribute, ICombinatorialValuesProvider
{
    // TargetFrameworks.All is never mutated, so it's safe to hand the same array back on every call.
    public object?[] GetValues(ParameterInfo _) => TargetFrameworks.All;
}

/// <summary>
/// A <see cref="ICombinatorialValuesProvider"/> that yields every metadata mode under test
/// (<see cref="AcceptanceTestBase.MetadataModesToRun"/>): the runtime reflection path and, unless
/// disabled via the source-gen kill-switch, both source-generated paths. Apply it to a
/// <see cref="MetadataMode"/> parameter of a <c>[CombinatorialData]</c> test method so the same
/// assertions run against every metadata path. Prefer it over auto-expanding the enum, which would
/// ignore the kill-switch and always emit all three values.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class MetadataModeValuesAttribute : Attribute, ICombinatorialValuesProvider
{
    // MetadataModesToRun never changes during a run, so box it into an object?[] once and hand the
    // same array back on every call to avoid repeated allocations during combinatorial expansion.
    private static readonly object?[] BoxedValues = [.. AcceptanceTestBase.MetadataModesToRun.Cast<object?>()];

    public object?[] GetValues(ParameterInfo _) => BoxedValues;
}
