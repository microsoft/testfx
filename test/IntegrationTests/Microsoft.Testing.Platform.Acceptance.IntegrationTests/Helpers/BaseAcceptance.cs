// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using NuGet.Packaging;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// All the properties of this class should be non static.
/// At the moment are static because we need to share them between perclass/id fixtures and
/// it's not supported at the moment.
/// </summary>
public abstract class BaseAcceptanceTests : TestBase
{
    internal static string RID { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    private static readonly string MsTestCurrentVersion = string.Empty;

    static BaseAcceptanceTests()
    {
        foreach (var package in Directory.GetFiles(DotnetMuxer.ARTIFACTS_PACKAGES_NONSHIPPING, "*.nupkg", SearchOption.AllDirectories)
            .Union(Directory.GetFiles(DotnetMuxer.ARTIFACTS_PACKAGES_SHIPPING, "*.nupkg", SearchOption.AllDirectories)).Distinct())
        {
            using FileStream fs = File.OpenRead(package);
            var packageReader = new PackageArchiveReader(fs);
            var version = packageReader.NuspecReader.GetVersion();
            if (version.OriginalVersion is null)
            {
                throw new InvalidOperationException("Unexpected null nupkg version");
            }

            if (MsTestCurrentVersion == string.Empty)
            {
                MsTestCurrentVersion = version.OriginalVersion;
            }
            else
            {
                if (version.OriginalVersion != MsTestCurrentVersion)
                {
                    throw new InvalidOperationException($"Unexpected different package version found in the package folder {version.OriginalVersion} != {MsTestCurrentVersion}");
                }
            }
        }
    }

    public string MSTestCurrentVersion => MsTestCurrentVersion;

    protected BaseAcceptanceTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        AcceptanceFixture = acceptanceFixture;
    }

    public AcceptanceFixture AcceptanceFixture { get; }

    internal static IEnumerable<BuildConfiguration> GetBuildConfiguration()
    {
        string[] compilationModes = new[] { "Debug", "Release" };
        foreach (string compilationMode in compilationModes)
        {
            yield return compilationMode == "Debug" ? BuildConfiguration.Debug : BuildConfiguration.Release;
        }
    }

    internal static TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration)> FormatGetBuildMatrixTfmBuildConfigurationEntry(TestArgumentsContext ctx)
    {
        var entry = ((string, BuildConfiguration))ctx.Arguments;
        return new TestArgumentsEntry<(string, BuildConfiguration)>(entry, $"{entry.Item1},{entry.Item2}");
    }

    internal static IEnumerable<(string Tfm, BuildConfiguration BuildConfiguration)> GetBuildMatrixTfmBuildConfiguration()
    {
        foreach (TestArgumentsEntry<string> tfm in All_Tfms)
        {
            foreach (BuildConfiguration compilationMode in GetBuildConfiguration())
            {
                yield return (tfm.Arguments, compilationMode);
            }
        }
    }

    internal static IEnumerable<(string TargetFrameworksElementContent, BuildConfiguration BuildConfiguration)> GetBuildMatrixMultiTfm()
    {
        foreach (BuildConfiguration compilationMode in GetBuildConfiguration())
        {
            yield return (All_Tfms.ToTargetFrameworksElementContent(), compilationMode);
        }
    }
}
