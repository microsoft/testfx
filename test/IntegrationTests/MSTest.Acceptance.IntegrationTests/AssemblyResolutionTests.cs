// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class AssemblyResolutionTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public AssemblyResolutionTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    public async Task AssemblyResolution_WhenNotSpecified_TestFails()
    {
        TestHostResult testHostResult = await _testAssetFixture.TestHost.ExecuteAsync();

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains($"System.IO.FileNotFoundException: Could not load file or assembly '{TestAssetFixture.ProjectName}");
        testHostResult.AssertOutputContains("Failed! - Failed: 1, Passed: 0, Skipped: 0, Total: 1");
    }

    public async Task AssemblyResolution_WhenSpecified_TestSucceeds()
    {
        // Arrange
        string runSettingsFilePath = Path.Combine(_testAssetFixture.TestHost.DirectoryName, ".runsettings");
        File.WriteAllText(runSettingsFilePath, $"""
            <?xml version="1.0" encoding="utf-8"?>
            <RunSettings>
              <RunConfiguration>
              </RunConfiguration>
              <MSTestV2>
                <AssemblyResolution>
                  <Directory path="{_testAssetFixture.MainDllFolder.Path}" />
                </AssemblyResolution>
              </MSTestV2>
            </RunSettings>
            """);

        // Act
        TestHostResult testHostResult = await _testAssetFixture.TestHost.ExecuteAsync($"--settings {runSettingsFilePath}");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        testHostResult.AssertOutputDoesNotContain("System.IO.FileNotFoundException: Could not load file or assembly 'MSTest.Extensibility.Samples");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : IAsyncInitializable, IDisposable
    {
        public const string ProjectName = "AssemblyResolution.Main";
        private const string TargetFramework = "net6.0";
        private const string TestProjectName = "AssemblyResolution.Test";

        private readonly TempDirectory _testAssetDirectory = new();

        public TestHost TestHost { get; private set; } = null!;

        public TempDirectory MainDllFolder { get; private set; } = null!;

        public void Dispose()
        {
            _testAssetDirectory.Dispose();
            MainDllFolder?.Dispose();
        }

        public async Task InitializeAsync(InitializationContext context)
        {
            var solution = CreateTestAsset();
            var result = await DotnetCli.RunAsync($"build -nodeReuse:false {solution.SolutionFile} -c Release", acceptanceFixture.NuGetGlobalPackagesFolder.Path);
            Assert.AreEqual(0, result.ExitCode);

            TestHost = TestHost.LocateFrom(solution.Projects.Skip(1).Single().FolderPath, TestProjectName, TargetFramework);
            MainDllFolder = MoveMainDllToDifferentTempDirectory();
        }

        private VSSolution CreateTestAsset()
        {
            VSSolution solution = new(Path.Combine(_testAssetDirectory.Path, "MSTestSolution"), "MSTestSolution");
            solution.AddOrUpdateFileContent("NuGet.config", TestAsset.GetNuGetConfig(false, false));

            var mainProject = solution.CreateCSharpProject(ProjectName, TargetFramework);
            mainProject.AddOrUpdateFileContent(mainProject.ProjectFile, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFrameworks>{TargetFramework}</TargetFrameworks>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <UseAppHost>true</UseAppHost>
                        <LangVersion>preview</LangVersion>
                    </PropertyGroup>
                </Project>
                """);
            mainProject.AddOrUpdateFileContent("Class.cs", """
                namespace AssemblyResolution.Main;

                public class Class1
                {
                    public int Add(int a, int b) => a + b;
                }
                """);

            var testProject = solution.CreateCSharpProject(TestProjectName, TargetFramework);
            testProject.AddProjectReference(mainProject.ProjectFile);
            testProject.AddOrUpdateFileContent(testProject.ProjectFile, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFrameworks>{TargetFramework}</TargetFrameworks>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <OutputType>Exe</OutputType>
                        <UseAppHost>true</UseAppHost>
                        <LangVersion>preview</LangVersion>
                        <EnableMSTestRunner>true</EnableMSTestRunner>
                    </PropertyGroup>
                    <ItemGroup>
                        <PackageReference Include="Microsoft.Testing.Platform" Version="{MicrosoftTestingPlatformVersion}" />
                        <PackageReference Include="MSTest" Version="{MSTestVersion}" />
                    </ItemGroup>
                    <ItemGroup>
                        <ProjectReference Include="{mainProject.ProjectFile}" />
                    </ItemGroup>
                </Project>
                """);
            testProject.AddOrUpdateFileContent("UnitTest1.cs", $$"""
                using {{ProjectName}};

                using Microsoft.VisualStudio.TestTools.UnitTesting;

                namespace AssemblyResolution.Test;
                
                [TestClass]
                public class UnitTest1
                {
                    [TestMethod]
                    public void TestMethod1()
                    {
                        Assert.AreEqual(3, new Class1().Add(1, 2));
                    }
                }
                """);

            return solution;
        }

        private TempDirectory MoveMainDllToDifferentTempDirectory()
        {
            var sampleDllFilePath = Path.Combine(TestHost.DirectoryName, $"{ProjectName}.dll");
            Assert.IsTrue(File.Exists(sampleDllFilePath));

            TempDirectory tempDirectory2 = new();
            var newSampleDllFilePath = tempDirectory2.CopyFile(sampleDllFilePath);
            Assert.IsTrue(File.Exists(newSampleDllFilePath));

            File.Delete(sampleDllFilePath);
            Assert.IsFalse(File.Exists(sampleDllFilePath));

            return tempDirectory2;
        }
    }
}
