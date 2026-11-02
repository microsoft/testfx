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

    [TestMethod]
    [DataRow(0, "", DisplayName = "No properties")]
    [DataRow(1, " first ", DisplayName = "Single property")]
    [DataRow(2, " second, first ", DisplayName = "Multiple properties")]
    public void ToString_PropertyBagData_FormatsAllProperties(int count, string expectedProperties)
    {
        PropertyBagData data = new TestPropertyBagData("report", "diagnostic");
        AddProperties(data.Properties, count);

        Assert.AreEqual(
            $"PropertyBagData {{ DisplayName = report, Description = diagnostic, Properties = [{expectedProperties}] }}",
            data.ToString());
    }

    [TestMethod]
    [DataRow("diagnostic")]
    [DataRow(null)]
    public void FileArtifactProperty_Equality_UsesFileIdentityNameAndDescription(string? description)
    {
        FileInfo file = new("report.trx");
        FileArtifactProperty value = new(file, "results", description);
        FileArtifactProperty equal = new(file, "results", description);
        FileArtifactProperty[] different =
        [
            new(new FileInfo("report.trx"), "results", description),
            new(file, "other results", description),
            new(file, "results", "different description"),
        ];

        Assert.IsTrue(value.Equals(equal));
        Assert.IsTrue(value.Equals((object)equal));
        Assert.AreEqual(value.GetHashCode(), equal.GetHashCode());
        Assert.IsFalse(value.Equals(null));
        Assert.IsFalse(value.Equals(new object()));
        foreach (FileArtifactProperty other in different)
        {
            Assert.IsFalse(value.Equals(other));
            Assert.IsFalse(value.Equals((object)other));
        }

        Assert.AreEqual(
            $"FileArtifactProperty {{ FileInfo = report.trx, DisplayName = results, Description = {description} }}",
            value.ToString());
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

    private sealed class TestPropertyBagData(string displayName, string? description) : PropertyBagData(displayName, description);
}
