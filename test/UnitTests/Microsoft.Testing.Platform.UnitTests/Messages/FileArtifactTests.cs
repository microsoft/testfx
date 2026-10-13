// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileArtifactTests
{
    [TestMethod]
    [DataRow(0, "", DisplayName = "No properties")]
    [DataRow(1, " first ", DisplayName = "Single property")]
    [DataRow(2, " second, first ", DisplayName = "Multiple properties")]
    public void ToString_FileArtifact_FormatsProperties(int count, string expectedProperties)
    {
        string path = Path.Combine(Path.GetTempPath(), "testfx-artifact.trx");
        var artifact = new FileArtifact(new FileInfo(path), "report", "diagnostic", "microsoft.testing.trx");
        AddProperties(artifact.Properties, count);

        string actual = artifact.ToString();

        Assert.AreEqual(
            $"FileArtifact {{ DisplayName = report, Description = diagnostic, FilePath = {path}, Properties = [{expectedProperties}] }}",
            actual);
    }

    [TestMethod]
    [DataRow(0, "", DisplayName = "No properties")]
    [DataRow(1, " first ", DisplayName = "Single property")]
    [DataRow(2, " second, first ", DisplayName = "Multiple properties")]
    public void ToString_SessionFileArtifact_FormatsProperties(int count, string expectedProperties)
    {
        string path = Path.Combine(Path.GetTempPath(), "testfx-artifact.trx");
        var artifact = new SessionFileArtifact(new SessionUid("session-42"), new FileInfo(path), "report", "diagnostic", "microsoft.testing.trx");
        AddProperties(artifact.Properties, count);

        string actual = artifact.ToString();

        Assert.AreEqual(
            $"SessionFileArtifact {{ DisplayName = report, Description = diagnostic, FilePath = {path}, Properties = [{expectedProperties}], SessionUid =session-42, FilePath = {path} }}",
            actual);
    }

    private static void AddProperties(PropertyBag properties, int count)
    {
        if (count > 0)
        {
            properties.Add(new ArtifactProperty("first"));
        }

        if (count > 1)
        {
            properties.Add(new ArtifactProperty("second"));
        }
    }

    private sealed class ArtifactProperty(string text) : IProperty
    {
        public override string ToString() => text;
    }
}
