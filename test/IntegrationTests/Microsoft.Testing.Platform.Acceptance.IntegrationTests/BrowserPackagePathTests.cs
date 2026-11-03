// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class BrowserPackagePathTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/tr/", false, DisplayName = "SatellitePath_UnixLeadingSeparator")]
    [DataRow("\\tr\\", false, DisplayName = "SatellitePath_WindowsLeadingSeparator")]
    [DataRow("tr/", false, DisplayName = "SatellitePath_UnixRelativeDirectory")]
    [DataRow("tr\\", false, DisplayName = "SatellitePath_WindowsRelativeDirectory")]
    [DataRow("/tr/", true, DisplayName = "SatellitePath_UnixLeadingSeparatorWithTelemetry")]
    [DataRow("\\tr\\", true, DisplayName = "SatellitePath_WindowsLeadingSeparatorWithTelemetry")]
    [DataRow("tr/", true, DisplayName = "SatellitePath_UnixRelativeDirectoryWithTelemetry")]
    [DataRow("tr\\", true, DisplayName = "SatellitePath_WindowsRelativeDirectoryWithTelemetry")]
    public async Task SatellitePackagePath_NormalizesProductionMetadata(string recursiveDirectory, bool verbose)
    {
        string browserProject = Path.Combine(
            RootFinder.Find(),
            "src",
            "Platform",
            "Microsoft.Testing.Platform.Browser",
            "Microsoft.Testing.Platform.Browser.csproj");
        XElement satellite = XDocument.Load(browserProject).Descendants("TfmSpecificPackageFile")
            .Single(static item => (string?)item.Attribute("Include") == "$(TargetDir)**\\$(AssemblyName).resources.dll");
        XAttribute? packagePath = satellite.Attribute("PackagePath");
        Assert.IsNotNull(packagePath);

        using TempDirectory directory = new();
        string probe = Path.Combine(directory.Path, "satellite-path.proj");
        new XDocument(
            new XElement(
                "Project",
                new XElement(
                    "PropertyGroup",
                    new XElement(
                        "SatellitePackagePath",
                        packagePath.Value.Replace("%(RecursiveDir)", recursiveDirectory, StringComparison.Ordinal)))))
            .Save(probe);

        DotnetMuxerResult result = await DotnetCli.RunAsync(
            $"msbuild \"{probe}\" -nologo -getProperty:SatellitePackagePath -bl:\"{Path.Combine(directory.Path, "probe.binlog")}\"",
            workingDirectory: directory.Path,
            environmentVariables: new() { ["DOTNET_CLI_CONTEXT_VERBOSE"] = verbose ? "1" : null },
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.AreEqual(
            "tools/net8.0/any/tr/",
            BrowserPackageExecutionTests.GetLastNonEmptyOutputLine(result.StandardOutput).Trim().Replace('\\', '/'));
    }
}
