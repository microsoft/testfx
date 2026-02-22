// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class FileArtifactCopyDataConsumerTests : IDisposable
{
    private readonly string _resultsDirectory;
    private readonly string _sourceDirectory;
    private readonly FileArtifactCopyDataConsumer _consumer;

    public FileArtifactCopyDataConsumerTests()
    {
        _resultsDirectory = Path.Combine(Path.GetTempPath(), $"TestResults_{Guid.NewGuid():N}");
        _sourceDirectory = Path.Combine(Path.GetTempPath(), $"TestSource_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_resultsDirectory);
        Directory.CreateDirectory(_sourceDirectory);

        Mock<IConfiguration> configMock = new();
        configMock.Setup(c => c[PlatformConfigurationConstants.PlatformResultDirectory]).Returns(_resultsDirectory);

        _consumer = new FileArtifactCopyDataConsumer(configMock.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_resultsDirectory))
        {
            Directory.Delete(_resultsDirectory, recursive: true);
        }

        if (Directory.Exists(_sourceDirectory))
        {
            Directory.Delete(_sourceDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task IsEnabledAsync_ReturnsTrue()
    {
        bool isEnabled = await _consumer.IsEnabledAsync();

        Assert.IsTrue(isEnabled);
    }

    [TestMethod]
    public async Task ConsumeAsync_WithNoFileArtifacts_DoesNotCopyAnything()
    {
        TestNodeUpdateMessage message = CreateMessage(new PropertyBag(new PassedTestNodeStateProperty()));

        await _consumer.ConsumeAsync(new DummyProducer(), message, CancellationToken.None);

        Assert.AreEqual(0, Directory.GetFiles(_resultsDirectory).Length);
    }

    [TestMethod]
    public async Task ConsumeAsync_WithFileArtifact_CopiesFileToResultsDirectory()
    {
        string sourceFile = CreateSourceFile("TestOutput.txt", "test content");
        TestNodeUpdateMessage message = CreateMessage(new PropertyBag(
            new PassedTestNodeStateProperty(),
            new FileArtifactProperty(new FileInfo(sourceFile), "TestOutput.txt")));

        await _consumer.ConsumeAsync(new DummyProducer(), message, CancellationToken.None);

        string expectedDestination = Path.Combine(_resultsDirectory, "TestOutput.txt");
        Assert.IsTrue(File.Exists(expectedDestination));
        Assert.AreEqual("test content", File.ReadAllText(expectedDestination));
    }

    [TestMethod]
    public async Task ConsumeAsync_WithFileAlreadyInResultsDirectory_DoesNotCopyAgain()
    {
        string fileInResults = Path.Combine(_resultsDirectory, "AlreadyHere.txt");
        File.WriteAllText(fileInResults, "already here");

        TestNodeUpdateMessage message = CreateMessage(new PropertyBag(
            new PassedTestNodeStateProperty(),
            new FileArtifactProperty(new FileInfo(fileInResults), "AlreadyHere.txt")));

        await _consumer.ConsumeAsync(new DummyProducer(), message, CancellationToken.None);

        Assert.AreEqual(1, Directory.GetFiles(_resultsDirectory).Length);
        Assert.AreEqual("already here", File.ReadAllText(fileInResults));
    }

    [TestMethod]
    public async Task ConsumeAsync_WithNonTestNodeUpdateMessage_DoesNothing()
    {
        Mock<IData> dataMock = new();

        await _consumer.ConsumeAsync(new DummyProducer(), dataMock.Object, CancellationToken.None);

        Assert.AreEqual(0, Directory.GetFiles(_resultsDirectory).Length);
    }

    [TestMethod]
    public async Task ConsumeAsync_WithDuplicateFileName_AppendsCounter()
    {
        // Pre-create a file in the results directory with the same name.
        File.WriteAllText(Path.Combine(_resultsDirectory, "Duplicate.txt"), "original");

        string sourceFile = CreateSourceFile("Duplicate.txt", "new content");
        TestNodeUpdateMessage message = CreateMessage(new PropertyBag(
            new PassedTestNodeStateProperty(),
            new FileArtifactProperty(new FileInfo(sourceFile), "Duplicate.txt")));

        await _consumer.ConsumeAsync(new DummyProducer(), message, CancellationToken.None);

        string expectedDestination = Path.Combine(_resultsDirectory, "Duplicate_1.txt");
        Assert.IsTrue(File.Exists(expectedDestination));
        Assert.AreEqual("new content", File.ReadAllText(expectedDestination));
    }

    private string CreateSourceFile(string name, string content)
    {
        string path = Path.Combine(_sourceDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static TestNodeUpdateMessage CreateMessage(PropertyBag properties)
        => new(
            default,
            new TestNode
            {
                Uid = new TestNodeUid("test-id"),
                DisplayName = "TestMethod",
                Properties = properties,
            });

    private sealed class DummyProducer : IDataProducer
    {
        public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

        public string Uid => nameof(DummyProducer);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }
}
