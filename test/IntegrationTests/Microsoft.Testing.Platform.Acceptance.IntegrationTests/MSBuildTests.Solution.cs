// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestGroup]
public class MSBuildTests_Solution : AcceptanceTestBase
{
    private readonly AcceptanceFixture _acceptanceFixture;
    private const string AssetName = "MSTestProject";

    static MSBuildTests_Solution()
    {
        string dotnetMuxerSDK = Directory.GetDirectories(Path.Combine(RootFinder.Find(), ".dotnet", "sdk")).OrderByDescending(x => x).First();
        File.WriteAllText(Path.Combine(dotnetMuxerSDK, "Microsoft.Common.CrossTargeting.targets"), MicrosoftCommonCrossTargeting);
        if (!File.Exists(Path.Combine(dotnetMuxerSDK, "Microsoft.Common.Test.targets")))
        {
            File.WriteAllText(Path.Combine(dotnetMuxerSDK, "Microsoft.Common.Test.targets"), MicrosoftCommonTesttargets);
        }
    }

    public MSBuildTests_Solution(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        _acceptanceFixture = acceptanceFixture;
    }

    private void CheckPatch()
    {
        // https://github.com/dotnet/sdk/issues/37712
        if (DateTime.UtcNow.Date > new DateTime(2024, 9, 1))
        {
            throw new InvalidOperationException("Check if we can remove the patch!");
        }
    }

    internal static IEnumerable<TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>> GetBuildMatrix()
    {
        foreach (TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm)> entry in GetBuildMatrixSingleAndMultiTfmBuildConfiguration())
        {
            foreach (string command in new string[]
            {
                "build --no-restore -t:Test -p:UseMSBuildTestInfrastructure=true",
                "test --no-restore",
            })
            {
                yield return new TestArgumentsEntry<(string SingleTfmOrMultiTfm, BuildConfiguration BuildConfiguration, bool IsMultiTfm, string Command)>(
                (entry.Arguments.SingleTfmOrMultiTfm, entry.Arguments.BuildConfiguration, entry.Arguments.IsMultiTfm, command), $"{(entry.Arguments.IsMultiTfm ? "multitfm" : entry.Arguments.SingleTfmOrMultiTfm)},{entry.Arguments.BuildConfiguration},{command}");
            }
        }
    }

    [ArgumentsProvider(nameof(GetBuildMatrix))]
    [NonTest] // TODO: AMAURY/MARCO - Investigate test
    public async Task MSBuildTests_UseMSBuildTestInfrastructure_Should_Run_Solution_Tests(string singleTfmOrMultiTfm, BuildConfiguration _, bool isMultiTfm, string command)
    {
        CheckPatch();

        using TestAsset generator = await TestAsset.GenerateAssetAsync(
            AssetName,
            SourceCode
            .PatchCodeWithReplace("$TargetFrameworks$", isMultiTfm ? $"<TargetFrameworks>{singleTfmOrMultiTfm}</TargetFrameworks>" : $"<TargetFramework>{singleTfmOrMultiTfm}</TargetFramework>")
            .PatchCodeWithReplace("$MicrosoftTestingPlatformVersion$", MicrosoftTestingPlatformVersion)
            .PatchCodeWithReplace("$MicrosoftTestingEnterpriseExtensionsVersion$", MicrosoftTestingEnterpriseExtensionsVersion));

        string projectContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "MSBuildTests.csproj", SearchOption.AllDirectories).Single());
        string programSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Program.cs", SearchOption.AllDirectories).Single());
        string unitTestSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "UnitTest1.cs", SearchOption.AllDirectories).Single());
        string usingsSourceContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "Usings.cs", SearchOption.AllDirectories).Single());
        string nugetConfigContent = File.ReadAllText(Directory.GetFiles(generator.TargetAssetPath, "NuGet.config", SearchOption.AllDirectories).Single());

        // Create a solution with 3 projects
        using TempDirectory tempDirectory = new();
        string solutionFolder = Path.Combine(tempDirectory.Path, "Solution");
        VSSolution solution = new(solutionFolder, "MSTestSolution");
        string nugetFile = solution.AddOrUpdateFileContent("Nuget.config", nugetConfigContent);
        for (int i = 0; i < 3; i++)
        {
            CSharpProject project = solution.CreateCSharpProject($"TestProject{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            File.WriteAllText(project.ProjectFile, projectContent);
            project.AddOrUpdateFileContent("Program.cs", programSourceContent.PatchCodeWithReplace("$ProjectName$", $"TestProject{i}"));
            project.AddOrUpdateFileContent("UnitTest1.cs", unitTestSourceContent);
            project.AddOrUpdateFileContent("Usings.cs", usingsSourceContent);

            CSharpProject project2 = solution.CreateCSharpProject($"Project{i}", isMultiTfm ? singleTfmOrMultiTfm.Split(';') : [singleTfmOrMultiTfm]);
            project.AddProjectReference(project2.ProjectFile);
        }

        // Build the solution
        DotnetMuxerResult restoreResult = await DotnetCli.RunAsync($"restore -nodeReuse:false {solution.SolutionFile} --configfile {nugetFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);
        restoreResult.AssertOutputNotContains("An approximate best match of");
        DotnetMuxerResult testResult = await DotnetCli.RunAsync($"{command} -nodeReuse:false {solution.SolutionFile}", _acceptanceFixture.NuGetGlobalPackagesFolder.Path);

        if (isMultiTfm)
        {
            foreach (string tfm in singleTfmOrMultiTfm.Split(';'))
            {
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{tfm}\|x64\]");
                testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{tfm}\|x64\]");
            }
        }
        else
        {
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject0\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject1\..*' \[{singleTfmOrMultiTfm}\|x64\]");
            testResult.AssertOutputRegEx($@"Tests succeeded: '.*TestProject2\..*' \[{singleTfmOrMultiTfm}\|x64\]");
        }
    }

    private const string SourceCode = """
#file MSBuildTests.csproj
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        $TargetFrameworks$
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <OutputType>Exe</OutputType>
        <UseAppHost>true</UseAppHost>
        <LangVersion>preview</LangVersion>
        <PlatformTarget>x64</PlatformTarget>
        <GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
        <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Testing.Platform.MSBuild" Version="$MicrosoftTestingPlatformVersion$" />
        <!-- Platform and TrxReport.Abstractions are only needed because Internal.Framework relies on a preview version that we want to override with currently built one -->
        <PackageReference Include="Microsoft.Testing.Platform" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Extensions.TrxReport.Abstractions" Version="$MicrosoftTestingPlatformVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
        <PackageReference Include="Microsoft.Testing.Internal.Framework.SourceGeneration" Version="$MicrosoftTestingEnterpriseExtensionsVersion$" />
    </ItemGroup>
</Project>

#file Program.cs
using MSBuildTests;
using $ProjectName$;
ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
builder.AddTestFramework(new SourceGeneratedTestNodesBuilder());
builder.AddMSBuild();
using ITestApplication app = await builder.BuildAsync();
return await app.RunAsync();

#file UnitTest1.cs
namespace MSBuildTests;

[TestGroup]
public class UnitTest1
{
    public void TestMethod1()
    {
        Assert.IsTrue(true);
    }
}

#file Usings.cs
global using Microsoft.Testing.Platform.Builder;
global using Microsoft.Testing.Internal.Framework;
global using Microsoft.Testing.Platform.MSBuild;
""";

    private const string MicrosoftCommonCrossTargeting = """
<!--
***********************************************************************************************
Microsoft.Common.CrossTargeting.targets

WARNING:  DO NOT MODIFY this file unless you are knowledgeable about MSBuild and have
          created a backup copy.  Incorrect changes to this file will make it
          impossible to load or build your projects from the command-line or the IDE.

Copyright (C) Microsoft Corporation. All rights reserved.
***********************************************************************************************
-->
<Project DefaultTargets="Build">
  <PropertyGroup>
    <BuildInParallel Condition="'$(BuildInParallel)' == ''">true</BuildInParallel>
    <ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets Condition="'$(ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets)' == ''">true</ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.CrossTargeting.targets\ImportBefore\*.targets" Condition="'$(ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets)' == 'true' and exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.CrossTargeting.targets\ImportBefore')" />
  <Import Project="$(CustomBeforeMicrosoftCommonCrossTargetingTargets)" Condition="'$(CustomBeforeMicrosoftCommonCrossTargetingTargets)' != '' and Exists('$(CustomBeforeMicrosoftCommonCrossTargetingTargets)')" />
  <Target Name="GetTargetFrameworks" DependsOnTargets="GetTargetFrameworksWithPlatformFromInnerBuilds" Returns="@(_ThisProjectBuildMetadata)">
    <Error Condition="'$(IsCrossTargetingBuild)' != 'true'" Text="Internal MSBuild error: CrossTargeting GetTargetFrameworks target should only be used in cross targeting (outer) build" />
    <CombineXmlElements RootElementName="AdditionalProjectProperties" XmlElements="@(_TargetFrameworkInfo-&gt;'%(AdditionalPropertiesFromProject)')">
      <Output TaskParameter="Result" PropertyName="_AdditionalPropertiesFromProject" />
    </CombineXmlElements>
    <ItemGroup>
      <_ThisProjectBuildMetadata Include="$(MSBuildProjectFullPath)">
        <TargetFrameworks>@(_TargetFrameworkInfo)</TargetFrameworks>
        <TargetFrameworkMonikers>@(_TargetFrameworkInfo-&gt;'%(TargetFrameworkMonikers)')</TargetFrameworkMonikers>
        <TargetPlatformMonikers>@(_TargetFrameworkInfo-&gt;'%(TargetPlatformMonikers)')</TargetPlatformMonikers>
        <AdditionalPropertiesFromProject>$(_AdditionalPropertiesFromProject)</AdditionalPropertiesFromProject>
        <HasSingleTargetFramework>false</HasSingleTargetFramework>
        <IsRidAgnostic>@(_TargetFrameworkInfo-&gt;'%(IsRidAgnostic)')</IsRidAgnostic>
        <!-- Extract necessary information for SetPlatform negotiation -->
        <!-- This target does not run for cpp projects. -->
        <IsVcxOrNativeProj>false</IsVcxOrNativeProj>
        <Platform Condition="$([MSBuild]::AreFeaturesEnabled('17.4'))">$(Platform)</Platform>
        <Platforms>$(Platforms)</Platforms>
      </_ThisProjectBuildMetadata>
    </ItemGroup>
  </Target>
  <Target Name="_ComputeTargetFrameworkItems" Returns="@(InnerOutput)">
    <ItemGroup>
      <_TargetFramework Include="$(TargetFrameworks)" />
      <!-- Make normalization explicit: Trim; Deduplicate by keeping first occurrence, case insensitive -->
      <_TargetFrameworkNormalized Include="@(_TargetFramework-&gt;Trim()-&gt;Distinct())" />
      <_InnerBuildProjects Include="$(MSBuildProjectFile)">
        <AdditionalProperties>TargetFramework=%(_TargetFrameworkNormalized.Identity)</AdditionalProperties>
      </_InnerBuildProjects>
    </ItemGroup>
  </Target>
  <Target Name="GetTargetFrameworksWithPlatformFromInnerBuilds" DependsOnTargets="_ComputeTargetFrameworkItems">
    <MSBuild Projects="@(_InnerBuildProjects)" Condition="'@(_InnerBuildProjects)' != '' " Targets="GetTargetFrameworksWithPlatformForSingleTargetFramework" BuildInParallel="$(BuildInParallel)">
      <Output ItemName="_TargetFrameworkInfo" TaskParameter="TargetOutputs" />
    </MSBuild>
  </Target>
  <!--
    Target that allows targets consuming source control confirmation to establish a dependency on targets producing this information.

    Any target that reads SourceRevisionId, PrivateRepositoryUrl, SourceRoot, and other source control properties and items
    should depend on this target and be conditioned on '$(SourceControlInformationFeatureSupported)' == 'true'.

    SourceRevisionId property uniquely identifies the source control revision of the repository the project belongs to.
    For Git repositories this id is a commit hash, for TFVC repositories it's the changeset number, etc.

    PrivateRepositoryUrl property stores the URL of the repository supplied by the CI server or retrieved from source control manager.
    Targets consuming this property shall not publish its value implicitly as it might inadvertently reveal an internal URL.
    Instead, they shall only do so if the project sets PublishRepositoryUrl property to true. For example, the NuGet Pack target
    may include the repository URL in the nuspec file generated for NuGet package produced by the project if PublishRepositoryUrl is true.

    SourceRoot item group lists all source roots that the project source files reside under and their mapping to source control server URLs,
    if available. This includes both source files under source control as well as source files in source packages. SourceRoot items are
    used by compilers to determine path map in deterministic build and by SourceLink provider, which maps local paths to URLs of source files
    stored on the source control server.

    Source control information provider that sets these properties and items shall execute before this target (by including
    InitializeSourceControlInformation in its BeforeTargets) and set source control properties and items that haven't been initialized yet.
  -->
  <Target Name="InitializeSourceControlInformation" />
  <PropertyGroup>
    <SourceControlInformationFeatureSupported>true</SourceControlInformationFeatureSupported>
  </PropertyGroup>
  <!--
  ============================================================
                                       DispatchToInnerBuilds

     Builds this project with /t:$(InnerTarget) /p:TargetFramework=X for each
     value X in $(TargetFrameworks)

     [IN]
     $(TargetFrameworks) - Semicolon delimited list of target frameworks.
     $(InnerTargets) - The targets to build for each target framework

     [OUT]
     @(InnerOutput) - The combined output items of the inner targets across
                      all target frameworks..
  ============================================================
  -->
  <Target Name="DispatchToInnerBuilds" DependsOnTargets="_ComputeTargetFrameworkItems" Returns="@(InnerOutput)">
    <!-- If this logic is changed, also update Clean -->
    <MSBuild Projects="@(_InnerBuildProjects)" Condition="'@(_InnerBuildProjects)' != '' " Targets="$(InnerTargets)" BuildInParallel="$(BuildInParallel)">
      <Output ItemName="InnerOutput" TaskParameter="TargetOutputs" />
    </MSBuild>
  </Target>
  <!--
  ============================================================
                                       Build

   Cross-targeting version of Build.

   [IN]
   $(TargetFrameworks) - Semicolon delimited list of target frameworks.

   $(InnerTargets)     - The targets to build for each target framework. Defaults
                         to 'Build' if unset, but allows override to support
                         `msbuild /p:InnerTargets=X;Y;Z` which will build X, Y,
                         and Z targets for each target framework.

   [OUT]
   @(InnerOutput) - The combined output items of the inner targets across
                    all builds.
  ============================================================
  -->
  <Target Name="Build" DependsOnTargets="_SetBuildInnerTarget;DispatchToInnerBuilds" />
  <Target Name="_SetBuildInnerTarget" Returns="@(InnerOutput)">
    <PropertyGroup Condition="'$(InnerTargets)' == ''">
      <InnerTargets>Build</InnerTargets>
    </PropertyGroup>
  </Target>
  <!--
  ============================================================
                                       Clean

   Cross-targeting version of clean.

   Inner-build dispatch is a clone of DispatchToInnerBuilds;
   the only reason it's replicated is that it must be a different
   target to be run in the same build (e.g. by Rebuild or by
   a /t:Clean;Build invocation.
  ============================================================
  -->
  <Target Name="Clean" DependsOnTargets="_ComputeTargetFrameworkItems">
    <!-- If this logic is changed, also update DispatchToInnerBuilds -->
    <MSBuild Projects="@(_InnerBuildProjects)" Condition="'@(_InnerBuildProjects)' != '' " Targets="Clean" BuildInParallel="$(BuildInParallel)" />
  </Target>
  <!--
  ============================================================
                                       Rebuild

   Cross-targeting version of rebuild.
  ============================================================
  -->
  <Target Name="Rebuild" DependsOnTargets="Clean;Build" />
  <!--
    This will import NuGet restore targets. We need restore to work before any package assets are available.
  -->
  <PropertyGroup>
    <MSBuildUseVisualStudioDirectoryLayout Condition="'$(MSBuildUseVisualStudioDirectoryLayout)'==''">$([MSBuild]::IsRunningFromVisualStudio())</MSBuildUseVisualStudioDirectoryLayout>
    <NuGetRestoreTargets Condition="'$(NuGetRestoreTargets)'=='' and '$(MSBuildUseVisualStudioDirectoryLayout)'=='true'">$([MSBuild]::GetToolsDirectory32())\..\..\..\Common7\IDE\CommonExtensions\Microsoft\NuGet\NuGet.targets</NuGetRestoreTargets>
    <NuGetRestoreTargets Condition="'$(NuGetRestoreTargets)'==''">$(MSBuildToolsPath)\NuGet.targets</NuGetRestoreTargets>
  </PropertyGroup>
  <Import Project="$(NuGetRestoreTargets)" />
  <Import Project="$(CustomAfterMicrosoftCommonCrossTargetingTargets)" Condition="'$(CustomAfterMicrosoftCommonCrossTargetingTargets)' != '' and Exists('$(CustomAfterMicrosoftCommonCrossTargetingTargets)')" />
  <!--
    Allow extensions like NuGet restore to work before any package assets are available.
  -->
  <PropertyGroup>
    <ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets Condition="'$(ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets)' == ''">true</ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.CrossTargeting.targets\ImportAfter\*.targets" Condition="'$(ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets)' == 'true' and exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.CrossTargeting.targets\ImportAfter')" />
  <!--
    Import project extensions which usually come from packages.  Package management systems will create a file at:
      $(MSBuildProjectExtensionsPath)\$(MSBuildProjectFile).<SomethingUnique>.targets

    Each package management system should use a unique moniker to avoid collisions.  It is a wild-card iport so the package
    management system can write out multiple files but the order of the import is alphabetic because MSBuild sorts the list.

    This is the same import that would happen in an inner (non-cross targeting) build. Package management systems are responsible for generating
    appropriate conditions based on $(IsCrossTargetingBuild) to pull in only those package targets that are meant to participate in a cross-targeting
    build.
  -->
  <PropertyGroup>
    <ImportProjectExtensionTargets Condition="'$(ImportProjectExtensionTargets)' == ''">true</ImportProjectExtensionTargets>
  </PropertyGroup>
  <Import Project="$(MSBuildProjectExtensionsPath)$(MSBuildProjectFile).*.targets" Condition="'$(ImportProjectExtensionTargets)' == 'true' and exists('$(MSBuildProjectExtensionsPath)')" />
  <PropertyGroup>
    <ImportDirectoryBuildTargets Condition="'$(ImportDirectoryBuildTargets)' == ''">true</ImportDirectoryBuildTargets>
  </PropertyGroup>
  <!--
        Determine the path to the directory build targets file if the user did not disable $(ImportDirectoryBuildTargets) and
        they did not already specify an absolute path to use via $(DirectoryBuildTargetsPath)
    -->
  <PropertyGroup Condition="'$(ImportDirectoryBuildTargets)' == 'true' and '$(DirectoryBuildTargetsPath)' == ''">
    <_DirectoryBuildTargetsFile Condition="'$(_DirectoryBuildTargetsFile)' == ''">Directory.Build.targets</_DirectoryBuildTargetsFile>
    <_DirectoryBuildTargetsBasePath Condition="'$(_DirectoryBuildTargetsBasePath)' == ''">$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildProjectDirectory), '$(_DirectoryBuildTargetsFile)'))</_DirectoryBuildTargetsBasePath>
    <DirectoryBuildTargetsPath Condition="'$(_DirectoryBuildTargetsBasePath)' != '' and '$(_DirectoryBuildTargetsFile)' != ''">$([System.IO.Path]::Combine('$(_DirectoryBuildTargetsBasePath)', '$(_DirectoryBuildTargetsFile)'))</DirectoryBuildTargetsPath>
  </PropertyGroup>
  <Import Project="$(DirectoryBuildTargetsPath)" Condition="'$(ImportDirectoryBuildTargets)' == 'true' and exists('$(DirectoryBuildTargetsPath)')" />
  <PropertyGroup>
    <UseMSBuildTestInfrastructure Condition="'$(UseMSBuildTestInfrastructure)' == ''">false</UseMSBuildTestInfrastructure>
  </PropertyGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.Test.targets" Condition="'$(UseMSBuildTestInfrastructure)' == 'true'" />
</Project>
""";

    private const string MicrosoftCommonTesttargets = """
<Project>
    <Target Name="Test"></Target>
</Project>
""";
}
