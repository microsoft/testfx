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
public abstract partial class BaseAcceptanceTests : TestBase
{
    [GeneratedRegex("^(.*?)\\.(?=(?:[0-9]+\\.){2,}[0-9]+(?:-[a-z]+)?\\.nupkg)(.*?)\\.nupkg$")]
    private static partial Regex ParseNugetPacakgeFileName();

    internal static string RID { get; private set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

    public static string MSTestCurrentVersion { get; private set; }

    static BaseAcceptanceTests()
    {
        var mstestTestFrameworkPackage = Path.GetFileName(Directory.GetFiles(Constants.ArtifactsPackagesShipping, "MSTest.TestFramework*.nupkg", SearchOption.AllDirectories).Single());
        Match match = ParseNugetPacakgeFileName().Match(mstestTestFrameworkPackage);
        if (!match.Success)
        {
            throw new InvalidOperationException("Package version not found");
        }

        MSTestCurrentVersion = match.Groups[2].Value;
    }

    protected BaseAcceptanceTests(ITestExecutionContext testExecutionContext, AcceptanceFixture acceptanceFixture)
        : base(testExecutionContext)
    {
        AcceptanceFixture = acceptanceFixture;
    }

    public AcceptanceFixture AcceptanceFixture { get; }

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
}
