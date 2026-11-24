// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.OutputDevice.Terminal;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestFrameworkDisplayTests
{
    [TestMethod]
    [DataRow(null, null, null, "", DisplayName = "NoMetadata")]
    [DataRow("", " ", " ", "", DisplayName = "EmptyMetadata")]
    [DataRow("net472", null, null, "net472", DisplayName = "TargetOnly")]
    [DataRow(null, "x64", null, "x64", DisplayName = "ArchitectureOnly")]
    [DataRow(null, null, "net8.0", "runtime: net8.0", DisplayName = "RuntimeOnly")]
    [DataRow(null, "x64", "net8.0", "x64, runtime: net8.0", DisplayName = "UnknownTarget")]
    [DataRow("net472", "x64", "net472", "net472|x64", DisplayName = "MatchingFramework")]
    [DataRow("net472", "x64", "net481", "net472|x64, runtime: net481", DisplayName = "FrameworkRollForward")]
    [DataRow("net471", "x86", "net472", "net471|x86, runtime: net472", DisplayName = "FrameworkReleaseDifference")]
    [DataRow("net48", "x64", "net481", "net48|x64, runtime: net481", DisplayName = "Framework481IsDistinct")]
    [DataRow("net8.0-windows10.0.19041.0", "x64", "net8.0", "net8.0-windows10.0.19041.0|x64", DisplayName = "PlatformIsNotRuntimeDifference")]
    [DataRow("net10.0", "arm64", "net10.0", "net10.0|arm64", DisplayName = "MatchingModernRuntime")]
    [DataRow("net8.0", "x64", "NET8.0", "net8.0|x64", DisplayName = "ComparisonIgnoresCase")]
    [DataRow("net8.0", "x64", "net9.0", "net8.0|x64, runtime: net9.0", DisplayName = "ModernRollForward")]
    [DataRow("netcoreapp3.1", "x64", "netcoreapp3.1", "netcoreapp3.1|x64", DisplayName = "MatchingNetCoreRuntime")]
    [DataRow("net472", null, "net481", "net472, runtime: net481", DisplayName = "NoArchitecture")]
    [DataRow("net8.0", "x64", "Custom runtime", "net8.0|x64, runtime: Custom runtime", DisplayName = "UnknownRuntime")]
    [DataRow("Custom", "x64", "Custom", "Custom|x64, runtime: Custom", DisplayName = "UnknownIsNotAssumedEqual")]
    public void GetDisplayName_KeepsTargetAndLabelsOnlyMeaningfulRuntimeDifferences(
        string? targetFramework, string? architecture, string? runtimeFramework, string expected)
        => Assert.AreEqual(expected, TestFrameworkDisplay.GetDisplayName(targetFramework, architecture, runtimeFramework));
}
