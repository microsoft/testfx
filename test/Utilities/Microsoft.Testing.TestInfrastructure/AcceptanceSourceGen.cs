// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// Shared helpers for building the <see cref="MetadataMode.SourceGeneration"/> variant of an
/// acceptance-test asset. The <c>MSTest.SourceGeneration</c> package is a plain Roslyn source
/// generator: referencing it (together with <c>MSTest.TestAdapter</c>, which carries the runtime
/// hook types) is all that is needed to switch an asset onto the source-generated metadata path.
///
/// <para>
/// To avoid rewriting every generated <c>.csproj</c>, we inject the package reference through an
/// MSBuild <c>CustomBeforeMicrosoftCommonProps</c> file that is only passed on the source-gen
/// build command line. The normal (reflection) build never sees it. The two builds are sent to
/// distinct output folders so both test hosts coexist on disk.
/// </para>
/// </summary>
public static class AcceptanceSourceGen
{
    private const string SourceGenPropsFileName = "MSTest.AcceptanceSourceGen.props";

    private const string NuGetPackageExtensionName = ".nupkg";

    private const string SourceGenerationPackagePrefix = "MSTest.SourceGeneration.";

    /// <summary>
    /// The <c>BaseOutputPath</c>/<c>BaseIntermediateOutputPath</c> sub-folder used to keep the
    /// source-gen build outputs separate from the reflection build (which uses the default
    /// <c>bin</c>/<c>obj</c>).
    /// </summary>
    public const string OutputSubFolder = "SourceGen";

    /// <summary>
    /// Global kill-switch. When this environment variable is set to a truthy value, the source-gen
    /// build variant is skipped everywhere (useful while ramping up or when debugging locally).
    /// </summary>
    public const string SkipEnvironmentVariable = "MSTEST_ACCEPTANCE_SKIP_SOURCEGEN";

    private static readonly Lazy<string> LazyVersion = new(ResolveSourceGenerationVersion);

    /// <summary>
    /// Gets the version of the locally built <c>MSTest.SourceGeneration</c> shipping package.
    /// </summary>
    public static string Version => LazyVersion.Value;

    /// <summary>
    /// Gets a value indicating whether the source-gen build variant is globally disabled through
    /// <see cref="SkipEnvironmentVariable"/>.
    /// </summary>
    public static bool IsGloballyDisabled
    {
        get
        {
            string? value = Environment.GetEnvironmentVariable(SkipEnvironmentVariable);
            return value is not null
                && (value == "1"
                    || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static readonly string PropsContent =
$$"""
<Project>
  <!--
    Injected only for the MetadataMode.SourceGeneration build of an acceptance-test asset.
    Referencing MSTest.SourceGeneration switches the asset onto the source-generated metadata path.
  -->
  <PropertyGroup>
    <!--
      This build redirects bin/obj to bin/SourceGen and obj/SourceGen (so it does not overwrite the
      reflection build). That redirect drops the original obj/bin from DefaultItemExcludes, which would
      let the reflection build's generated *.cs (AssemblyInfo, MicrosoftTestingPlatformEntryPoint,
      SelfRegisteredExtensions) be double-compiled into this build. Re-exclude them here. This runs in
      CustomBeforeMicrosoftCommonProps (before the SDK seeds DefaultItemExcludes), so the SDK appends
      its own excludes after ours.
    -->
    <DefaultItemExcludes>$(DefaultItemExcludes);obj/**;bin/**</DefaultItemExcludes>
  </PropertyGroup>
  <ItemGroup>
    <!--
      Source generation is .NET-only: the generated metadata references ModuleInitializerAttribute /
      DynamicallyAccessedMembersAttribute / DynamicDependencyAttribute, which do not compile on .NET
      Framework. Skip the generator on net4x legs of a multi-targeting asset; those legs build as plain
      reflection and are simply not exercised in SourceGeneration mode by the test matrix. In the inner
      build TargetFramework is already set as a global property, so the condition resolves per TFM (it is
      empty for the outer cross-targeting build, which only dispatches and does not compile).
    -->
    <PackageReference Include="MSTest.SourceGeneration" Version="$(MSTestSourceGenerationVersion)"
                      Condition=" !$(TargetFramework.StartsWith('net4')) " />
  </ItemGroup>
</Project>
""";

    /// <summary>
    /// Writes the injection props file into the asset directory and returns the MSBuild command-line
    /// arguments (without a leading space) needed to produce the source-gen build of that asset.
    /// </summary>
    /// <param name="assetDirectory">The directory containing the generated asset (project) files.</param>
    /// <returns>The extra arguments to append to a <c>dotnet build</c> command.</returns>
    public static async Task<string> PrepareBuildArgumentsAsync(string assetDirectory)
    {
        string propsPath = Path.Combine(assetDirectory, SourceGenPropsFileName);
        await TempDirectory.WriteFileAsync(assetDirectory, SourceGenPropsFileName, PropsContent);

        // - CustomBeforeMicrosoftCommonProps injects the MSTest.SourceGeneration PackageReference
        //   early enough for restore to see it, without clobbering any Directory.Build.props.
        // - BaseOutputPath/BaseIntermediateOutputPath redirect bin/obj so this build does not
        //   overwrite the reflection build outputs. Forward slashes are used intentionally: a
        //   trailing backslash immediately before the closing quote would escape the quote on
        //   Windows and corrupt the argument. MSBuild accepts forward slashes on all platforms.
        // - MSTestSourceGenerationVersion feeds the version into the injected props.
        return $"-p:CustomBeforeMicrosoftCommonProps=\"{propsPath}\" "
            + $"-p:BaseOutputPath=\"bin/{OutputSubFolder}/\" "
            + $"-p:BaseIntermediateOutputPath=\"obj/{OutputSubFolder}/\" "
            + $"-p:MSTestSourceGenerationVersion={Version}";
    }

    private static string ResolveSourceGenerationVersion()
    {
        string rootFolder = Constants.ArtifactsPackagesShipping;
        string[] matches = Directory.Exists(rootFolder)
            ? Directory.GetFiles(rootFolder, SourceGenerationPackagePrefix + "*" + NuGetPackageExtensionName, SearchOption.TopDirectoryOnly)
            : [];

        if (matches.Length > 1)
        {
            // The prefix can match neighboring packages; keep only those where a version digit
            // follows the prefix (mirrors AcceptanceTestBase.ExtractVersionFromPackage).
            matches = [.. matches
                .Select(path => (path, fileName: Path.GetFileName(path)[SourceGenerationPackagePrefix.Length..]))
                .Where(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _))
                .Select(tuple => tuple.path)];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Was expecting to find a single NuGet package named '{SourceGenerationPackagePrefix}' in '{rootFolder}', but found {matches.Length}: '{string.Join("', '", matches.Select(Path.GetFileName))}'. Did you run with -pack?");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(
            SourceGenerationPackagePrefix.Length,
            packageFullName.Length - SourceGenerationPackagePrefix.Length - NuGetPackageExtensionName.Length);
    }
}
