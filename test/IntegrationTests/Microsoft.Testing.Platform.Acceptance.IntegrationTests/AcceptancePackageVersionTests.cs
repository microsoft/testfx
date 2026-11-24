// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class AcceptancePackageVersionTests : AcceptanceTestBase
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PackageVersions_MatchEvaluatedCentralPackageVersions()
    {
        DotnetMuxerResult result = await DotnetCli.RunAsync(
            "msbuild Directory.Packages.props -getItem:PackageVersion",
            workingDirectory: Constants.Root,
            environmentVariables: new() { ["DOTNET_CLI_CONTEXT_VERBOSE"] = "false" },
            cancellationToken: TestContext.CancellationToken);

        using var evaluation = JsonDocument.Parse(result.StandardOutput);
        var versions = evaluation.RootElement.GetProperty("Items").GetProperty("PackageVersion")
            .EnumerateArray()
            .ToDictionary(
                package => package.GetProperty("Identity").GetString()!,
                package => package.GetProperty("Version").GetString(),
                StringComparer.Ordinal);

        Assert.AreEqual(versions["Microsoft.Extensions.Hosting"], MicrosoftExtensionsHostingVersion);
        Assert.AreEqual(versions["OpenTelemetry"], OpenTelemetryVersion);
    }
}
