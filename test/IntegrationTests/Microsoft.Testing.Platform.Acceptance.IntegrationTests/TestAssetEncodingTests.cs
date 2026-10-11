// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestAssetEncodingTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    public async Task GeneratedSourceFilesHaveUtf8Bom()
    {
        using TempDirectory tempDirectory = new();
        using TestAsset testAsset = await TestAsset.GenerateAssetAsync(
            "SourceFilesHaveUtf8Bom",
            """
            #file Program.cs
            class Program { }
            #file Program.csx
            class Script { }
            #file Program.vb
            Class Program
            End Class
            #file Program.vbx
            Class Script
            End Class
            #file Data.txt
            data
            """,
            tempDirectory,
            addDefaultNuGetConfigFile: false);

        byte[] expectedPreamble = Encoding.UTF8.GetPreamble();
        foreach (string fileName in new[] { "Program.cs", "Program.csx", "Program.vb", "Program.vbx" })
        {
            byte[] contents = await File.ReadAllBytesAsync(Path.Combine(testAsset.TargetAssetPath, fileName), TestContext.CancellationToken);
            Assert.AreSequenceEqual(expectedPreamble, contents[..expectedPreamble.Length], fileName);
        }

        byte[] dataContents = await File.ReadAllBytesAsync(Path.Combine(testAsset.TargetAssetPath, "Data.txt"), TestContext.CancellationToken);
        Assert.AreNotSequenceEqual(expectedPreamble, dataContents[..expectedPreamble.Length], "Data.txt");
    }
}
