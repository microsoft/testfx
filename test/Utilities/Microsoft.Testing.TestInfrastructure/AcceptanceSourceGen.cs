// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// Shared helpers for building the source-generated variants of an acceptance-test asset. Both
/// variants are produced by the single shipping <c>MSTest.SourceGeneration</c> package, which hosts
/// two generators selected through the <c>MSTestSourceGenMode</c> MSBuild property. Each is exposed
/// as a <see cref="MetadataMode"/>:
/// <list type="bullet">
///   <item><see cref="MetadataMode.SourceGeneration"/> builds with the <c>Rooting</c> mode.</item>
///   <item><see cref="MetadataMode.AotSourceGeneration"/> builds with <c>MSTestSourceGenMode=ReflectionFree</c>.</item>
/// </list>
/// Referencing the package (together with <c>MSTest.TestAdapter</c>, which carries the runtime hook
/// types) is all that is needed to switch an asset onto the matching source-generated metadata path.
///
/// <para>
/// To avoid rewriting every generated <c>.csproj</c>, we inject the package reference through an
/// MSBuild <c>CustomBeforeMicrosoftCommonProps</c> file that is only passed on the source-gen
/// build command line. The normal (reflection) build never sees it. Each variant is sent to a
/// distinct output folder so the hosts coexist on disk.
/// </para>
/// </summary>
public static class AcceptanceSourceGen
{
    private const string NuGetPackageExtensionName = ".nupkg";

    private const string ShippingSourceGenerationPackagePrefix = "MSTest.SourceGeneration.";

    /// <summary>
    /// Global kill-switch. When this environment variable is set to a truthy value, the source-gen
    /// build variants are skipped everywhere (useful while ramping up or when debugging locally).
    /// </summary>
    public const string SkipEnvironmentVariable = "MSTEST_ACCEPTANCE_SKIP_SOURCEGEN";

    private static readonly Lazy<string> LazyShippingVersion =
        new(() => ResolveVersion(Constants.ArtifactsPackagesShipping, ShippingSourceGenerationPackagePrefix));

    /// <summary>
    /// Gets a value indicating whether the source-gen build variants are globally disabled through
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

    /// <summary>
    /// Gets the <c>bin</c>/<c>obj</c> sub-folder used to keep a source-gen build's outputs separate
    /// from the reflection build (which uses the default <c>bin</c>/<c>obj</c>) and from the other
    /// source-gen build.
    /// </summary>
    /// <param name="metadataMode">The source-gen metadata mode.</param>
    /// <returns>The output sub-folder name (for example <c>SourceGen</c>).</returns>
    public static string GetOutputSubFolder(MetadataMode metadataMode)
        => metadataMode switch
        {
            MetadataMode.SourceGeneration => "SourceGen",
            MetadataMode.AotSourceGeneration => "AotSourceGen",
            _ => throw new ArgumentOutOfRangeException(nameof(metadataMode), metadataMode, "Not a source-gen metadata mode."),
        };

    /// <summary>
    /// Writes the injection props file into the asset directory and returns the MSBuild command-line
    /// arguments (without a leading space) needed to produce the source-gen build of that asset for
    /// the given <paramref name="metadataMode"/>.
    /// </summary>
    /// <param name="assetDirectory">The directory containing the generated asset (project) files.</param>
    /// <param name="metadataMode">The source-gen metadata mode to build.</param>
    /// <returns>The extra arguments to append to a <c>dotnet build</c> command.</returns>
    public static async Task<string> PrepareBuildArgumentsAsync(string assetDirectory, MetadataMode metadataMode)
    {
        // Both source-gen variants ship in the single MSTest.SourceGeneration package; the mode is
        // selected at build time through MSTestSourceGenMode rather than by referencing a different
        // package. ReflectionFree is the shipped default (see MSTest.TestAdapter.targets), so we set
        // the mode explicitly for BOTH variants here to keep each test independent of the product
        // default and to keep the Rooting path exercised even though it is no longer the default.
        string outputSubFolder = GetOutputSubFolder(metadataMode);
        const string packageId = "MSTest.SourceGeneration";
        const string versionProperty = "MSTestSourceGenerationVersion";
        string version = LazyShippingVersion.Value;

        string propsFileName = $"MSTest.AcceptanceSourceGen.{outputSubFolder}.props";
        string propsPath = Path.Combine(assetDirectory, propsFileName);
        await TempDirectory.WriteFileAsync(assetDirectory, propsFileName, BuildPropsContent(packageId, versionProperty));

        // MSTestSourceGenMode selects which generator emits inside the same package. It is surfaced to
        // the compiler as a CompilerVisibleProperty by MSTest.TestAdapter.targets, so a global -p: is
        // sufficient. ReflectionFree is now the product default, but we pass Rooting explicitly for the
        // Rooting variant so it stays covered regardless of the default.
        string sourceGenModeArg = metadataMode == MetadataMode.AotSourceGeneration
            ? " -p:MSTestSourceGenMode=ReflectionFree"
            : " -p:MSTestSourceGenMode=Rooting";

        // - CustomBeforeMicrosoftCommonProps injects the source-generator PackageReference early
        //   enough for restore to see it, without clobbering any Directory.Build.props.
        // - BaseOutputPath/BaseIntermediateOutputPath redirect bin/obj so this build does not
        //   overwrite the reflection build (or the other source-gen build) outputs. Forward slashes
        //   are used intentionally: a trailing backslash immediately before the closing quote would
        //   escape the quote on Windows and corrupt the argument. MSBuild accepts forward slashes on
        //   all platforms.
        // - The version property feeds the resolved package version into the injected props.
        return $"-p:CustomBeforeMicrosoftCommonProps=\"{propsPath}\" "
            + $"-p:BaseOutputPath=\"bin/{outputSubFolder}/\" "
            + $"-p:BaseIntermediateOutputPath=\"obj/{outputSubFolder}/\" "
            + $"-p:{versionProperty}={version}"
            + sourceGenModeArg;
    }

    private static string BuildPropsContent(string packageId, string versionProperty) =>
$$"""
<Project>
  <!--
    Injected only for a source-generation build of an acceptance-test asset.
    Referencing {{packageId}} switches the asset onto the matching source-generated metadata path.
  -->
  <PropertyGroup>
    <!--
      This build redirects bin/obj to an isolated sub-folder (so it does not overwrite the reflection
      build or the other source-gen build). That redirect drops the original obj/bin from
      DefaultItemExcludes, which would let another build's generated *.cs (AssemblyInfo,
      MicrosoftTestingPlatformEntryPoint, SelfRegisteredExtensions) be double-compiled into this build.
      Re-exclude them here. This runs in CustomBeforeMicrosoftCommonProps (before the SDK seeds
      DefaultItemExcludes), so the SDK appends its own excludes after ours.

      The nested '**/obj/**;**/bin/**' globs matter for multi-project assets. The reflection build (which
      always runs first, into the default bin/Release) leaves a referenced project's outputs on disk, e.g.
      'SomeRef/bin/Release/<tfm>/*.dll'. When this (later) source-gen build evaluates the host project, the
      SDK's default item globs would otherwise pull those stray DLLs in as None/Content items, and RAR then
      treats a wrong-TFM copy (for example a net10.0 'MSTest.TestFramework.Extensions.dll', assembly version
      9.0.0.0) as a candidate for the net8.0 leg -> MSB3277, which MSBuildTreatWarningsAsErrors promotes to an
      error. The root-level 'bin/**;obj/**' does not match the nested 'SomeRef/bin/**', so exclude nested
      bin/obj as well to keep a referenced project's leftover reflection outputs out of this build's globs.
    -->
    <DefaultItemExcludes>$(DefaultItemExcludes);obj/**;bin/**;**/obj/**;**/bin/**</DefaultItemExcludes>
  </PropertyGroup>
  <ItemGroup>
    <!--
      Source generation is .NET-only: the generated metadata references ModuleInitializerAttribute /
      DynamicallyAccessedMembersAttribute / DynamicDependencyAttribute, which do not compile on .NET
      Framework. Skip the generator on net4x legs of a multi-targeting asset; those legs build as plain
      reflection and are simply not exercised in source-gen mode by the test matrix. In the inner build
      TargetFramework is already set as a global property, so the condition resolves per TFM (it is
      empty for the outer cross-targeting build, which only dispatches and does not compile).
    -->
    <PackageReference Include="{{packageId}}" Version="$({{versionProperty}})"
                      Condition=" !$(TargetFramework.StartsWith('net4')) " />
  </ItemGroup>
</Project>
""";

    private static string ResolveVersion(string rootFolder, string packagePrefix)
    {
        string[] matches = Directory.Exists(rootFolder)
            ? Directory.GetFiles(rootFolder, packagePrefix + "*" + NuGetPackageExtensionName, SearchOption.TopDirectoryOnly)
            : [];

        if (matches.Length > 1)
        {
            // The prefix can match neighboring packages; keep only those where a version digit
            // follows the prefix (mirrors AcceptanceTestBase.ExtractVersionFromPackage).
            matches = [.. matches
                .Select(path => (path, fileName: Path.GetFileName(path)[packagePrefix.Length..]))
                .Where(tuple => int.TryParse(tuple.fileName[0].ToString(), CultureInfo.InvariantCulture, out _))
                .Select(tuple => tuple.path)];
        }

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Was expecting to find a single NuGet package named '{packagePrefix}' in '{rootFolder}', but found {matches.Length}: '{string.Join("', '", matches.Select(Path.GetFileName))}'. Did you run with -pack?");
        }

        string packageFullName = Path.GetFileName(matches[0]);
        return packageFullName.Substring(
            packagePrefix.Length,
            packageFullName.Length - packagePrefix.Length - NuGetPackageExtensionName.Length);
    }
}
