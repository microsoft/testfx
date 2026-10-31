// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class FileUtilityContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public FileUtilityContinuationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void CopyOverwritesExistingContentAndCreatesMissingDirectoryChain()
    {
        using FileDirectory fixture = new();
        string source = fixture.Write("source.txt", "replacement");
        string existing = fixture.Write("existing.txt", "original");
        string nested = Path.Combine(fixture.Root, "new", "nested", "copy.txt");
        FileUtility utility = new();

        utility.CopyFileOverwrite(source, existing, out string? overwriteWarning).Should().Be(existing);
        utility.CopyFileOverwrite(source, nested, out string? directoryWarning).Should().Be(nested);

        overwriteWarning.Should().BeNull();
        directoryWarning.Should().BeNull();
        File.ReadAllText(existing).Should().Be("replacement");
        File.ReadAllText(nested).Should().Be("replacement");
        File.ReadAllText(source).Should().Be("replacement");
        Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Should().BeEquivalentTo([source, existing, nested]);
    }

    public void NextIterationSkipsFilesAndDirectoriesWithoutCreatingCandidate()
    {
        using FileDirectory fixture = new();
        fixture.Write("Deploy", "existing file");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Deploy[1]"));
        fixture.Write("Deploy[2]", "another file");
        FileUtility utility = new();

        string candidate = utility.GetNextIterationDirectoryName(fixture.Root, "Deploy");

        candidate.Should().Be(Path.Combine(fixture.Root, "Deploy[3]"));
        File.Exists(candidate).Should().BeFalse();
        Directory.Exists(candidate).Should().BeFalse();
        File.ReadAllText(Path.Combine(fixture.Root, "Deploy")).Should().Be("existing file");
    }

    public void PdbDeploymentCopiesOnceRecordsSourceAndReportsConflicts()
    {
        using FileDirectory fixture = new();
        string source = fixture.Write(Path.Combine("source", "sample.dll"), "assembly");
        string sourcePdb = fixture.Write(Path.Combine("source", "sample.pdb"), "first symbols");
        string destination = fixture.Write(Path.Combine("out", "sample.dll"), "deployed assembly");
        string relative = Path.Combine("nested", "sample.dll");
        string relativePdb = Path.Combine("nested", "sample.pdb");
        string destinationPdb = Path.Combine(fixture.Root, "out", "sample.pdb");
        Dictionary<string, string> map = [with(StringComparer.OrdinalIgnoreCase)];
        FileUtility utility = new();

        utility.FindAndDeployPdb(destination, relative, source, map).Should().Be(destinationPdb);

        map.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>(relativePdb, sourcePdb));
        File.ReadAllText(destinationPdb).Should().Be("first symbols");
        File.WriteAllText(destinationPdb, "consumer changes");
        utility.FindAndDeployPdb(destination, relative, source, map).Should().BeNull();
        string otherSource = fixture.Write(Path.Combine("other", "sample.dll"), "other assembly");
        string otherPdb = fixture.Write(Path.Combine("other", "sample.pdb"), "other symbols");
        utility.FindAndDeployPdb(destination, relative, otherSource, map).Should().BeNull();

        File.ReadAllText(destinationPdb).Should().Be("consumer changes");
        map.Should().ContainSingle().Which.Value.Should().Be(sourcePdb);
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning("Conflict during copying PDBs for line number info: '{0}' and '{1}' are from different origins although they might be the same.", otherPdb, sourcePdb),
            Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("Pdb file found for path '{0}'", source), Times.Exactly(2));
    }

    public void FailedPdbCopyDoesNotReserveDestinationAndCanBeRetried()
    {
        using FileDirectory fixture = new();
        string source = fixture.Write(Path.Combine("source", "sample.dll"), "assembly");
        string sourcePdb = fixture.Write(Path.Combine("source", "sample.pdb"), "symbols");
        string destination = fixture.Write(Path.Combine("out", "sample.dll"), "assembly");
        Dictionary<string, string> map = [];
        FailingPdbCopy utility = new() { Fail = true };

        utility.FindAndDeployPdb(destination, "sample.dll", source, map).Should().BeNull();
        map.Should().BeEmpty();
        File.Exists(Path.ChangeExtension(destination, ".pdb")).Should().BeFalse();
        utility.Fail = false;
        utility.FindAndDeployPdb(destination, "sample.dll", source, map).Should().Be(Path.ChangeExtension(destination, ".pdb"));
        map.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>("sample.pdb", sourcePdb));
        File.ReadAllText(Path.ChangeExtension(destination, ".pdb")).Should().Be("symbols");
    }

    public void PdbValidationRejectsNonBinariesMissingAndInvalidSymbolSources()
    {
        using FileDirectory fixture = new();
        FileUtility utility = new();
        Dictionary<string, string> map = [];
        string destination = Path.Combine(fixture.Root, "out", "sample.dll");

        utility.FindAndDeployPdb(destination, "sample.dll", null!, map).Should().BeNull();
        utility.FindAndDeployPdb(destination, "sample.dll", "\0", map).Should().BeNull();
        utility.FindAndDeployPdb(destination, "sample.dll", Path.Combine(fixture.Root, "missing.dll"), map).Should().BeNull();
        string ordinary = fixture.Write("sample.txt", "not a binary");
        fixture.Write("sample.pdb", "must not be deployed");
        utility.FindAndDeployPdb(Path.Combine(fixture.Root, "sample.txt"), "sample.txt", ordinary, map).Should().BeNull();

        map.Should().BeEmpty();
        Directory.Exists(Path.Combine(fixture.Root, "out")).Should().BeFalse();
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning("Path is either null or invalid. Path = '{0}'", It.Is<object?[]>(arguments => arguments.Length == 1 && arguments[0] == null)),
            Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Warning("Path is either null or invalid. Path = '{0}'", "\0"), Times.Once);
    }

    public void InvalidPdbDestinationIsReportedWithoutChangingMap()
    {
        using FileDirectory fixture = new();
        string source = fixture.Write("sample.dll", "assembly");
        fixture.Write("sample.pdb", "symbols");
#if NETFRAMEWORK
        string destination = Path.Combine(fixture.Root, "out", "sample.dll");
        string relative = "\0" + Path.DirectorySeparatorChar + "sample.dll";
#else
        string destination = fixture.Root + Path.DirectorySeparatorChar + "\0" + Path.DirectorySeparatorChar + "sample.dll";
        const string relative = "sample.dll";
#endif
        Dictionary<string, string> map = [];

        new FileUtility().FindAndDeployPdb(destination, relative, source, map).Should().BeNull();

        map.Should().BeEmpty();
#if NETFRAMEWORK
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning("Error while trying to locate pdb for deployed assembly '{0}': {1}", destination, It.IsAny<ArgumentException>()),
            Times.Once);
#endif
        File.ReadAllText(Path.ChangeExtension(source, ".pdb")).Should().Be("symbols");
    }

    public void RecursiveEnumerationIgnoresOnlyRequestedFileIoFailures()
    {
        Mock<FileUtility> utility = new() { CallBase = true };
        IOException unavailable = new("root file enumeration failed");
        utility.Setup(value => value.GetFilesInADirectory("root")).Throws(unavailable);
        utility.Setup(value => value.GetDirectoriesInADirectory("root")).Returns(["child"]);
        utility.Setup(value => value.GetFilesInADirectory("child")).Returns(["child/file.txt"]);
        utility.Setup(value => value.GetDirectoriesInADirectory("child")).Returns([]);

        utility.Object.AddFilesFromDirectory("root", true).Should().Equal("child/file.txt");
        Action strict = () => utility.Object.AddFilesFromDirectory("root", false);
        strict.Should().ThrowExactly<IOException>().Which.Should().BeSameAs(unavailable);

        utility.Setup(value => value.GetFilesInADirectory("root")).Returns(["root/file.txt"]);
        utility.Setup(value => value.GetFilesInADirectory("child")).Throws(new IOException("child file enumeration failed"));
        utility.Object.AddFilesFromDirectory("root", false).Should().Equal("root/file.txt");
        UnauthorizedAccessException denied = new("access denied");
        utility.Setup(value => value.GetFilesInADirectory("child")).Throws(denied);
        Action deniedChild = () => utility.Object.AddFilesFromDirectory("root", true);
        deniedChild.Should().ThrowExactly<UnauthorizedAccessException>().Which.Should().BeSameAs(denied);

        utility.Setup(value => value.GetFilesInADirectory("child")).Returns([]);
        utility.Setup(value => value.GetDirectoriesInADirectory("root")).Throws(unavailable);
        Action unavailableDirectories = () => utility.Object.AddFilesFromDirectory("root", true);
        unavailableDirectories.Should().ThrowExactly<IOException>().Which.Should().BeSameAs(unavailable);
    }

    public void DeletingDirectoriesRemovesContentAndReportsMissingRoots()
    {
        using FileDirectory fixture = new();
        string child = Directory.CreateDirectory(Path.Combine(fixture.Root, "delete")).FullName;
        File.WriteAllText(Path.Combine(child, "content.txt"), "owned content");
        FileUtility utility = new();

        utility.DeleteDirectories(child);
        Directory.Exists(child).Should().BeFalse();
        utility.DeleteDirectories(child);

        _provider.MockTraceLogger.Verify(
            logger => logger.Error("DeploymentManager.DeleteDirectories failed for the directory '{0}': {1}", child, It.IsAny<DirectoryNotFoundException>()),
            Times.Once);
        Directory.Exists(fixture.Root).Should().BeTrue();
    }

    public void FilesystemWrappersPreserveRealEntriesAndAttributes()
    {
        using FileDirectory fixture = new();
        FileUtility utility = new();
        string child = Path.Combine(fixture.Root, "child");
        utility.CreateDirectoryIfNotExists(child);
        string file = fixture.Write("entry.txt", "content");
        utility.CreateDirectoryIfNotExists(child);

        utility.GetFilesInADirectory(fixture.Root).Should().Equal(file);
        utility.GetDirectoriesInADirectory(fixture.Root).Should().Equal(child);
        utility.DoesFileExist(file).Should().BeTrue();
        utility.DoesDirectoryExist(child).Should().BeTrue();
        utility.DoesFileExist(child).Should().BeFalse();
        utility.DoesDirectoryExist(file).Should().BeFalse();
        try
        {
            utility.SetAttributes(file, FileAttributes.ReadOnly);
            (File.GetAttributes(file) & FileAttributes.ReadOnly).Should().Be(FileAttributes.ReadOnly);
            File.ReadAllText(file).Should().Be("content");
        }
        finally
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private sealed class FailingPdbCopy : FileUtility
    {
        public bool Fail { get; set; }

        public override string CopyFileOverwrite(string source, string destination, out string? warning)
        {
            if (Fail)
            {
                warning = "copy rejected by fixture";
                return string.Empty;
            }

            return base.CopyFileOverwrite(source, destination, out warning);
        }
    }

    private sealed class FileDirectory : IDisposable
    {
        public FileDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-files-" + Guid.NewGuid().ToString("N"));

        public string Write(string name, string content)
        {
            string path = Path.Combine(Root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
