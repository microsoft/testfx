// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class BrowserSdkSelectionTests
{
    private const string SelectedCommit = "1111111111111111111111111111111111111111";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ResolveSdkVersion_SelectsSourceMatchedSdkWhenMultipleVersionsExist()
    {
        string root = CreateSdkLayout(
            ("11.0.100-dev", "0000000000000000000000000000000000000000"),
            ("12.0.100-dev", SelectedCommit));

        string actual = BrowserSdkSelection.ResolveSdkVersion(
            root,
            "12.0.100-dev",
            SelectedCommit);

        Assert.AreEqual("12.0.100-dev", actual);
    }

    [TestMethod]
    public void ResolveSdkVersion_RejectsSdkBuiltFromDifferentCommit()
    {
        string root = CreateSdkLayout(
            ("11.0.100-dev", "0000000000000000000000000000000000000000"),
            ("12.0.100-dev", SelectedCommit));

        Assert.ThrowsExactly<AssertFailedException>(
            () => BrowserSdkSelection.ResolveSdkVersion(
                root,
                "12.0.100-dev",
                "2222222222222222222222222222222222222222"));
    }

    private string CreateSdkLayout(params (string Version, string Commit)[] sdks)
    {
        string root = Path.Combine(
            TestContext.TestRunResultsDirectory ?? Path.GetTempPath(),
            $"browser-sdk-selection-{Guid.NewGuid():N}");
        foreach ((string version, string commit) in sdks)
        {
            string directory = Path.Combine(root, "sdk", version);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ".toolsetversion"), commit);
        }

        return root;
    }
}
