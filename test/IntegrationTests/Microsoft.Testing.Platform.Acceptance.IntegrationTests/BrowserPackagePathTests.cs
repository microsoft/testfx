// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class BrowserPackagePathTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("/tr/", DisplayName = "SatellitePath_UnixLeadingSeparator")]
    [DataRow("\\tr\\", DisplayName = "SatellitePath_WindowsLeadingSeparator")]
    [DataRow("tr/", DisplayName = "SatellitePath_UnixRelativeDirectory")]
    [DataRow("tr\\", DisplayName = "SatellitePath_WindowsRelativeDirectory")]
    public async Task SatellitePackagePath_NormalizesProductionMetadata(string recursiveDirectory)
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
            failIfReturnValueIsNotZero: false,
            cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.ToString());
        Assert.AreEqual("tools/net8.0/any/tr/", result.StandardOutput.Trim().Replace('\\', '/'));
    }
}
