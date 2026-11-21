// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class FileUtilityMutationTests : TestContainer
{
    private const string PdbLocationWarning = "Error while trying to locate pdb for deployed assembly '{0}': {1}";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public FileUtilityMutationTests()
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

    public void CopyFileOverwriteCreatesTheDestinationDirectoryOnlyWhenTheSourceExists()
    {
        using FileDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.txt");
        string destinationDirectory = Path.Combine(fixture.Root, "created-on-demand");
        string destination = Path.Combine(destinationDirectory, "copy.txt");
        FileUtility utility = new();

        string failedResult = utility.CopyFileOverwrite(source, destination, out string? failedWarning);

        failedResult.Should().BeEmpty();
        failedWarning.Should().NotBeNullOrEmpty().And.Contain(source);
        Directory.Exists(destinationDirectory).Should().BeFalse();

        fixture.Write("source.txt", "content");
        string copiedResult = utility.CopyFileOverwrite(source, destination, out string? copiedWarning);

        copiedResult.Should().Be(destination);
        copiedWarning.Should().BeNull();
        File.ReadAllText(destination).Should().Be("content");
    }

    public void FindAndDeployPdbReportsARelativeDestinationWithoutParentDirectory()
    {
        using FileDirectory fixture = new();
        string source = fixture.Write("sample.dll", "assembly");
        fixture.Write("sample.pdb", "symbols");
        string destination = Path.Combine(fixture.Root, "out", "sample.dll");

        // A root has no parent directory, so combining it with the symbols file name fails.
        string rootRelativeDestination = Path.DirectorySeparatorChar.ToString();
        Dictionary<string, string> map = [];

        new FileUtility().FindAndDeployPdb(destination, rootRelativeDestination, source, map).Should().BeNull();

        map.Should().BeEmpty();
        Directory.Exists(Path.Combine(fixture.Root, "out")).Should().BeFalse();
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning(PdbLocationWarning, destination, It.IsAny<ArgumentException>()),
            Times.Once);
    }

    public void FindAndDeployPdbDoesNotReportARelativeDestinationWithoutParentDirectoryWhenWarningsAreDisabled()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(false);
        using FileDirectory fixture = new();
        string source = fixture.Write("sample.dll", "assembly");
        fixture.Write("sample.pdb", "symbols");
        string destination = Path.Combine(fixture.Root, "out", "sample.dll");
        Dictionary<string, string> map = [];

        new FileUtility().FindAndDeployPdb(destination, Path.DirectorySeparatorChar.ToString(), source, map).Should().BeNull();

        map.Should().BeEmpty();
        _provider.MockTraceLogger.Verify(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

    public void TryConvertPathToRelativeStripsTheRootDirectoryAndLeadingSeparators()
    {
        string rootDirectory = Path.Combine(Path.GetTempPath(), "testfx-root");
        string file = Path.Combine(rootDirectory, "sub", "file.txt");
        string expected = Path.Combine("sub", "file.txt");

        FileUtility.TryConvertPathToRelative(file, rootDirectory).Should().Be(expected);
        FileUtility.TryConvertPathToRelative(file, rootDirectory.ToUpperInvariant()).Should().Be(expected);
        FileUtility.TryConvertPathToRelative(rootDirectory, rootDirectory).Should().BeEmpty();
    }

    public void TryConvertPathToRelativeKeepsPathsThatAreNotRootedOrNotUnderTheRootDirectory()
    {
        string temp = Path.GetTempPath();
        string rootDirectory = Path.Combine(temp, "testfx-root");
        string outside = Path.Combine(temp, "testfx-other", "file.txt");
        string relative = Path.Combine("data", "file.txt");

        FileUtility.TryConvertPathToRelative(outside, rootDirectory).Should().Be(outside);
        FileUtility.TryConvertPathToRelative(relative, "data").Should().Be(relative);
    }

    public void DeleteDirectoriesRejectsNullAndWhitespacePathsWithoutLoggingAnError()
    {
        FileUtility utility = new();

        Action nullPath = () => utility.DeleteDirectories(null!);
        Action whitespacePath = () => utility.DeleteDirectories("  ");

        nullPath.Should().ThrowExactly<ArgumentNullException>().WithParameterName("filePath");
        whitespacePath.Should().ThrowExactly<ArgumentException>().WithParameterName("filePath");
        _provider.MockTraceLogger.Verify(logger => logger.Error(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

#if DEBUG && !NETFRAMEWORK
    public void CreateDirectoryIfNotExistsFailsTheDiagnosticAssertionForAnEmptyDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Action create = () => new FileUtility().CreateDirectoryIfNotExists(string.Empty);

        create.Should().Throw<ArgumentException>();
        assertions.Should().Equal("directory");
    }

    public void ReplaceInvalidFileNameCharactersFailsTheDiagnosticAssertionForAnEmptyName()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        FileUtility.ReplaceInvalidFileNameCharacters(string.Empty).Should().BeEmpty();

        assertions.Should().Equal("fileName");
    }

    public void GetNextIterationDirectoryNameFailsTheDiagnosticAssertionsForEmptyArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        FileUtility utility = new();
        string unique = "testfx-missing-" + Guid.NewGuid().ToString("N");
        string missingParent = Path.Combine(Path.GetTempPath(), unique);

        utility.GetNextIterationDirectoryName(string.Empty, unique).Should().Be(unique);
        assertions.Should().Equal("parentDirectoryName");

        assertions.Clear();
        utility.GetNextIterationDirectoryName(missingParent, string.Empty).Should().Be(missingParent);
        assertions.Should().Equal("originalDirectoryName");
    }

    public void CopyFileOverwriteFailsTheDiagnosticAssertionsForEmptyArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using FileDirectory fixture = new();
        string source = fixture.Write("source.txt", "content");
        string destination = Path.Combine(fixture.Root, "copy.txt");
        FileUtility utility = new();

        utility.CopyFileOverwrite(string.Empty, destination, out string? emptySourceWarning).Should().BeEmpty();
        emptySourceWarning.Should().NotBeNullOrEmpty();
        assertions.Should().Equal("source should not be null.", "DeploymentManager: source path  must be rooted!");

        assertions.Clear();
        utility.CopyFileOverwrite(source, string.Empty, out string? emptyDestinationWarning).Should().BeEmpty();
        emptyDestinationWarning.Should().NotBeNullOrEmpty();
        assertions.Should().Equal("destination should not be null.");
        File.Exists(destination).Should().BeFalse();
    }

    public void CopyFileOverwriteFailsTheDiagnosticAssertionForARelativeSource()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using FileDirectory fixture = new();
        string relativeSource = "testfx-missing-" + Guid.NewGuid().ToString("N") + ".txt";
        string destination = Path.Combine(fixture.Root, "copy.txt");

        new FileUtility().CopyFileOverwrite(relativeSource, destination, out string? warning).Should().BeEmpty();

        warning.Should().NotBeNullOrEmpty().And.Contain(relativeSource);
        assertions.Should().Equal("DeploymentManager: source path " + relativeSource + " must be rooted!");
    }

    public void FindAndDeployPdbFailsTheDiagnosticAssertionsForMissingArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        FileUtility utility = new();
        Dictionary<string, string> map = [];

        utility.FindAndDeployPdb(string.Empty, "relative.txt", "source.txt", map).Should().BeNull();
        assertions.Should().Equal("destination should not be null or empty.");

        assertions.Clear();
        utility.FindAndDeployPdb("destination.txt", string.Empty, "source.txt", map).Should().BeNull();
        assertions.Should().Equal("relative destination path should not be null or empty.");

        assertions.Clear();
        utility.FindAndDeployPdb("destination.txt", "relative.txt", "source.txt", null!).Should().BeNull();
        assertions.Should().Equal("destToSource should not be null.");

        map.Should().BeEmpty();
    }

    public void TryConvertPathToRelativeFailsTheDiagnosticAssertionsForEmptyArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        FileUtility.TryConvertPathToRelative(string.Empty, "root").Should().BeEmpty();
        assertions.Should().Equal("path should not be null or empty.");

        assertions.Clear();
        FileUtility.TryConvertPathToRelative("path", string.Empty).Should().Be("path");
        assertions.Should().Equal("rootDir should not be null or empty.");
    }

    public void ValidArgumentsDoNotFailTheDiagnosticAssertions()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using FileDirectory fixture = new();
        string source = fixture.Write("sample.dll", "assembly");
        fixture.Write("sample.pdb", "symbols");
        string destination = Path.Combine(fixture.Root, "out", "sample.dll");
        Dictionary<string, string> map = [];
        FileUtility utility = new();

        utility.CreateDirectoryIfNotExists(Path.Combine(fixture.Root, "created"));
        FileUtility.ReplaceInvalidFileNameCharacters("sample.dll").Should().Be("sample.dll");
        utility.GetNextIterationDirectoryName(fixture.Root, "Deploy").Should().Be(Path.Combine(fixture.Root, "Deploy"));
        utility.CopyFileOverwrite(source, destination, out string? warning).Should().Be(destination);
        utility.FindAndDeployPdb(destination, "sample.dll", source, map).Should().Be(Path.ChangeExtension(destination, ".pdb"));
        FileUtility.TryConvertPathToRelative(destination, fixture.Root).Should().Be(Path.Combine("out", "sample.dll"));

        warning.Should().BeNull();
        assertions.Should().BeEmpty();
    }
#endif

    private sealed class FileDirectory : IDisposable
    {
        public FileDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-mutation-files-" + Guid.NewGuid().ToString("N"));

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
