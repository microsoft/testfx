// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

/// <summary>
/// All the properties of this class should be non static.
/// At the moment are static because we need to share them between perclass/id fixtures and
/// it's not supported at the moment.
/// </summary>
public abstract partial class AcceptanceTestBase : TestBase
{
    [GeneratedRegex("^(.*?)\\.(?=(?:[0-9]+\\.){2,}[0-9]+(?:-[a-z]+)?\\.nupkg)(.*?)\\.nupkg$")]
    private static partial Regex ParseNuGetPackageFileNameRegex();

    internal static string RID { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    public static string MSTestCurrentVersion { get; private set; }

    static AcceptanceTestBase()
    {
        var mstestTestFrameworkPackage = Path.GetFileName(Directory.GetFiles(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework*.nupkg", SearchOption.AllDirectories).Single());
        Match match = ParseNuGetPackageFileNameRegex().Match(mstestTestFrameworkPackage);
        if (!match.Success)
        {
            throw new InvalidOperationException("Package version not found");
        }

        MSTestCurrentVersion = match.Groups[2].Value;
    }

    protected AcceptanceTestBase(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    internal static IEnumerable<TestArgumentsEntry<(string Tfm, BuildConfiguration BuildConfiguration, Verb Verb)>> GetBuildMatrixTfmBuildConfiguration()
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
}
