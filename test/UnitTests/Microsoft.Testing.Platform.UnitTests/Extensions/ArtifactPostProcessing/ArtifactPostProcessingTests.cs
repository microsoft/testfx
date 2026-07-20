// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests.Extensions.ArtifactPostProcessing;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

[TestClass]
public sealed class ArtifactPostProcessingTests
{
    [TestMethod]
    public void HandshakeProperties_AreSortedAndDeduplicated()
    {
        IArtifactPostProcessor[] processors =
        [
            new StubProcessor("first", ["z.kind", "a.kind"], [".TRX"]),
            new StubProcessor("second", ["a.kind"], [".trx", ".xml"]),
        ];

        IReadOnlyDictionary<byte, string> properties = ArtifactPostProcessingHandshakeProperties.Create(processors)!;

        Assert.AreEqual("a.kind;z.kind", properties[HandshakeMessagePropertyNames.SupportedPostProcessorKinds]);
        Assert.AreEqual(".trx;.xml", properties[HandshakeMessagePropertyNames.SupportedPostProcessorExtensionsLegacy]);
    }

    [TestMethod]
    public void HandshakeProperties_WithNoCapabilities_ReturnsNull()
        => Assert.IsNull(ArtifactPostProcessingHandshakeProperties.Create([new StubProcessor("empty", [], [])]));

    [TestMethod]
    public async Task Manager_BuildsOnlyEnabledProcessors()
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new StubProcessor("enabled", ["kind"], [".ext"]));
        manager.AddArtifactPostProcessor(_ => new StubProcessor("disabled", ["other"], [".other"], isEnabled: false));

        IReadOnlyList<IArtifactPostProcessor> processors = await manager.BuildAsync(new ServiceProvider());

        Assert.HasCount(1, processors);
        Assert.AreEqual("enabled", processors[0].Uid);
    }

    [DataRow("kind;other", ".ext")]
    [DataRow("kind", ".ext;.other")]
    [TestMethod]
    public async Task Manager_CapabilityContainingSeparator_ThrowsInvalidOperationException(string kind, string extension)
    {
        ArtifactPostProcessingManager manager = new();
        manager.AddArtifactPostProcessor(_ => new StubProcessor("processor", [kind], [extension]));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => manager.BuildAsync(new ServiceProvider()));
    }

    [TestMethod]
    public void Manifest_LoadsVersionedAttributedInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"artifact-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string manifestPath = Path.Combine(directory, "manifest.json");
        try
        {
            File.WriteAllText(
                manifestPath,
                $$"""
                {
                  "schemaVersion": 1,
                  "outputDirectory": "{{directory.Replace("\\", "\\\\")}}",
                  "inputs": [
                    {
                      "path": "a.trx",
                      "kind": "microsoft.testing.trx",
                      "producingTestModule": "A.dll",
                      "targetFramework": "net10.0",
                      "architecture": "x64",
                      "executionId": "execution"
                    },
                    {
                      "path": "legacy.trx",
                      "kind": null
                    }
                  ]
                }
                """);

            var manifest = ArtifactPostProcessingManifest.Load(manifestPath);

            Assert.AreEqual(directory, manifest.OutputDirectory);
            Assert.HasCount(2, manifest.Inputs);
            Assert.AreEqual("microsoft.testing.trx", manifest.Inputs[0].Kind);
            Assert.AreEqual("A.dll", manifest.Inputs[0].ProducingTestModule);
            Assert.IsNull(manifest.Inputs[1].Kind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Manifest_WithUnsupportedVersion_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, """{ "schemaVersion": 2, "outputDirectory": "out", "inputs": [] }""");

            FormatException exception = Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));

            Assert.AreEqual(Platform.Resources.PlatformResources.ArtifactPostProcessingManifestInvalid, exception.Message);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithMalformedJson_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, """{ "schemaVersion": 1, """);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithInputMissingPath_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                manifestPath,
                """{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{ "kind": "microsoft.testing.trx" }] }""");

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [{}] }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": [null] }""")]
    [TestMethod]
    public void Manifest_WithEmptyInputEntry_ThrowsFormatException(string json)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, json);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [TestMethod]
    public void Manifest_WithTopLevelArray_ThrowsFormatException()
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, "[]");

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out" }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": "bad" }""")]
    [DataRow("""{ "schemaVersion": 1, "outputDirectory": "out", "inputs": {} }""")]
    [TestMethod]
    public void Manifest_WithInputsThatIsNotArray_ThrowsFormatException(string json)
    {
        string manifestPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(manifestPath, json);

            Assert.ThrowsExactly<FormatException>(() => ArtifactPostProcessingManifest.Load(manifestPath));
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    private sealed class StubProcessor(
        string uid,
        IReadOnlyList<string> supportedKinds,
        IReadOnlyList<string> supportedExtensions,
        bool isEnabled = true) : IArtifactPostProcessor
    {
        public string Uid { get; } = uid;

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public IReadOnlyList<string> SupportedKinds { get; } = supportedKinds;

        public IReadOnlyList<string> SupportedFileExtensionsFallback { get; } = supportedExtensions;

        public Task<bool> IsEnabledAsync() => Task.FromResult(isEnabled);

        public Task<ProcessedArtifact?> ProcessAsync(
            IReadOnlyList<InputArtifact> inputs,
            string outputDirectory,
            CancellationToken cancellationToken)
            => Task.FromResult<ProcessedArtifact?>(null);
    }
}
