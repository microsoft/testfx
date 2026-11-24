// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class DeploymentUtilityBaseMutationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public DeploymentUtilityBaseMutationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void CreateDeploymentDirectoriesCreatesTheRootInOutAndMachineNameDirectories()
    {
        string results = Path.Combine(Path.GetTempPath(), "testfx-mutation-base-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(results, "Deploy");
        string inDirectory = Path.Combine(root, "In");
        string outDirectory = Path.Combine(root, "Out");
        string machineDirectory = Path.Combine(inDirectory, Environment.MachineName);
        Mock<FileUtility> files = new();
        ExposedDeployment utility = new(files.Object);

        TestRunDirectories directories = utility.CreateDeploymentDirectories(new DeploymentContext(results, null), null);

        directories.OutDirectory.Should().Be(outDirectory);
        files.Verify(value => value.CreateDirectoryIfNotExists(root), Times.Once);
        files.Verify(value => value.CreateDirectoryIfNotExists(inDirectory), Times.Once);
        files.Verify(value => value.CreateDirectoryIfNotExists(outDirectory), Times.Once);
        files.Verify(value => value.CreateDirectoryIfNotExists(machineDirectory), Times.Once);
        files.Verify(value => value.CreateDirectoryIfNotExists(It.IsAny<string>()), Times.Exactly(4));
    }

    public void DeployRejectsANullOrWhitespaceDeploymentDirectory()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object);

        Action nullDirectory = () => _ = utility.RunItems([], source, null!, fixture.Root);
        Action whitespaceDirectory = () => _ = utility.RunItems([], source, "  ", fixture.Root);

        nullDirectory.Should().ThrowExactly<ArgumentNullException>().WithParameterName("deploymentDirectory");
        whitespaceDirectory.Should().ThrowExactly<ArgumentException>().WithParameterName("deploymentDirectory");
    }

    public void DeployRejectsANullOrWhitespaceTestSource()
    {
        using DeploymentDirectory fixture = new();
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object);

        Action nullSource = () => _ = utility.RunItems([], null!, fixture.Root, fixture.Root);
        Action whitespaceSource = () => _ = utility.RunItems([], "  ", fixture.Root, fixture.Root);

        nullSource.Should().ThrowExactly<ArgumentNullException>().WithParameterName("testSourceHandler");
        whitespaceSource.Should().ThrowExactly<ArgumentException>().WithParameterName("testSourceHandler");
    }

    public void DeployReportsWhichDeploymentDirectoryDoesNotExist()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        string missing = Path.Combine(fixture.Root, "missing");
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesFileExist(source)).Returns(true);
        ExposedDeployment utility = new(files.Object);

        Action act = () => _ = utility.RunItems([], source, missing, fixture.Root);

        act.Should().ThrowExactly<InvalidOperationException>().Which.Message.Should().Be($"Deployment directory {missing} does not exist");
    }

    public void DeployReportsWhichTestSourceDoesNotExist()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesDirectoryExist(fixture.Root)).Returns(true);
        ExposedDeployment utility = new(files.Object);

        Action act = () => _ = utility.RunItems([], source, fixture.Root, fixture.Root);

        act.Should().ThrowExactly<InvalidOperationException>().Which.Message.Should().Be($"TestSource {source} does not exist.");
    }

    public void DeployAppliesTheTestSettingsToTheDeploymentItemsOfTheFullTestSourcePath()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.GetFullPath(Path.Combine(fixture.Root, "source.dll"));
        string unnormalizedSource = Path.Combine(fixture.Root, "bin", "..", "source.dll");
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object) { SettingWarning = "warning from the test setting" };
        List<DeploymentItem> items = [];

        IEnumerable<string> warnings = utility.RunItems(items, unnormalizedSource, fixture.Root, fixture.Root);

        warnings.Should().Equal("warning from the test setting");
        utility.SettingSources.Should().Equal(source);
        utility.SettingItems.Should().ContainSingle().Which.Should().BeSameAs(items);
    }

    public void DependenciesAreOnlyRequestedForPerTestDeploymentOfAssemblies()
    {
        using DeploymentDirectory fixture = new();
        string assembly = Path.Combine(fixture.Root, "main.dll");

        DependencyRequestsFor(fixture, DeploymentItemOriginType.PerTestDeployment, "main.dll").Should().Equal(assembly);
        DependencyRequestsFor(fixture, DeploymentItemOriginType.PerTestDeployment, "data.txt").Should().BeEmpty("only assemblies have dependencies");
        DependencyRequestsFor(fixture, DeploymentItemOriginType.TestStorage, "main.dll").Should().BeEmpty("only per-test deployment items request dependencies");
        DependencyRequestsFor(fixture, DeploymentItemOriginType.Dependency, "main.dll").Should().BeEmpty("only per-test deployment items request dependencies");
        DependencyRequestsFor(fixture, DeploymentItemOriginType.Satellite, "main.dll").Should().BeEmpty("only per-test deployment items request dependencies");
    }

#if !NETFRAMEWORK
    public void DeployReportsADestinationThatCannotBeResolvedAndDoesNotCopyTheFile()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        string deploymentDirectory = Path.Combine(fixture.Root, "deployed");
        string invalid = Path.Combine(fixture.Root, "\0invalid.txt");
        string valid = Path.Combine(fixture.Root, "valid.txt");
        string invalidDestination = Path.Combine(deploymentDirectory, "\0invalid.txt");
        string validDestination = Path.GetFullPath(Path.Combine(deploymentDirectory, "valid.txt"));
        Action resolve = () => _ = Path.GetFullPath(invalidDestination);
        ArgumentException failure = resolve.Should().Throw<ArgumentException>().Which;
        Mock<FileUtility> files = DeploymentFiles(deploymentDirectory);
        ExposedDeployment utility = new(files.Object);

        IEnumerable<string> warnings = utility.RunItems([new DeploymentItem(invalid), new DeploymentItem(valid)], source, deploymentDirectory, fixture.Root);

        warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToAccessFile, invalidDestination, failure.GetType(), failure.Message));
        string? warning;
        files.Verify(value => value.CopyFileOverwrite(invalid, It.IsAny<string>(), out warning), Times.Never);
        files.Verify(value => value.CopyFileOverwrite(valid, validDestination, out warning), Times.Once);
    }
#endif

    public void DeployCopiesTheSymbolsFileNextToADeployedAssembly()
    {
        using DeploymentDirectory fixture = new();
        string assembly = fixture.Write(Path.Combine("items", "main.dll"), "assembly");
        string symbols = fixture.Write(Path.Combine("items", "main.pdb"), "symbols");
        string source = Path.Combine(fixture.Root, "source.dll");
        string deploymentDirectory = Path.Combine(fixture.Root, "deployed");
        string deployedAssembly = Path.GetFullPath(Path.Combine(deploymentDirectory, "main.dll"));
        string deployedSymbols = Path.Combine(Path.GetDirectoryName(deployedAssembly)!, "main.pdb");
        Mock<FileUtility> files = DeploymentFiles(deploymentDirectory);
        string? warning = null;
        files.Setup(value => value.CopyFileOverwrite(assembly, deployedAssembly, out warning)).Returns(deployedAssembly);
        ExposedDeployment utility = new(files.Object);

        utility.RunItems([new DeploymentItem(assembly)], source, deploymentDirectory, fixture.Root).Should().BeEmpty();

        files.Verify(value => value.CopyFileOverwrite(assembly, deployedAssembly, out warning), Times.Once);
        files.Verify(value => value.CopyFileOverwrite(symbols, deployedSymbols, out warning), Times.Once);
    }

    public void FindingTheFilesOfADirectoryItemDoesNotIgnoreIOExceptions()
    {
        using DeploymentDirectory fixture = new();
        string itemDirectory = Path.Combine(fixture.Root, "items");
        string file = Path.Combine(itemDirectory, "file.txt");
        string results = Path.Combine(fixture.Root, "results");
        string source = Path.Combine(fixture.Root, "source.dll");
        Func<string, bool>? capturedPredicate = null;
        List<string> filesInDirectory = [file];
        Mock<FileUtility> files = DeploymentFiles(itemDirectory);
        files
            .Setup(value => value.AddFilesFromDirectory(itemDirectory, It.IsAny<Func<string, bool>?>(), false))
            .Callback<string, Func<string, bool>?, bool>((_, predicate, _) => capturedPredicate = predicate)
            .Returns(filesInDirectory);
        ExposedDeployment utility = new(files.Object);
        List<string> warnings = [];

        List<string>? found = utility.FindFiles(new DeploymentItem(itemDirectory), source, results, warnings, out bool isDirectory);

        isDirectory.Should().BeTrue();
        found.Should().Equal(file);
        warnings.Should().BeEmpty();
        files.Verify(value => value.AddFilesFromDirectory(itemDirectory, It.IsAny<Func<string, bool>?>(), false), Times.Once);
        capturedPredicate.Should().NotBeNull();
        Func<string, bool> ignoreDirectory = capturedPredicate!;
        ignoreDirectory(results).Should().BeTrue();
        ignoreDirectory(results.ToUpperInvariant()).Should().BeTrue();
        ignoreDirectory(Path.Combine(fixture.Root, "other")).Should().BeFalse();
    }

    public void ADeploymentItemSourcePathIsResolvedAgainstTheDirectoryOfTheTestSource()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-mutation-base-paths");
        string source = Path.Combine(root, "bin", "source.dll");
        string relative = Path.Combine("data", "file.txt");
        string rooted = Path.Combine(root, "data", "file.txt");
        string sourceWithoutDirectory = Path.GetPathRoot(Path.GetTempPath())!;

        ExposedDeployment.FullPathToSource(relative, source).Should().Be(Path.Combine(root, "bin", "data", "file.txt"));
        ExposedDeployment.FullPathToSource(rooted, source).Should().Be(rooted);

        // A rooted path never needs the directory of the test source, which does not exist for a drive or filesystem root.
        ExposedDeployment.FullPathToSource(rooted, sourceWithoutDirectory).Should().Be(rooted);
    }

#if DEBUG && !NETFRAMEWORK
    public void DeployFailsTheDiagnosticAssertionsForFilesThatAreNotRooted()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();

        _ = DeployDirectoryItem(fixture.Root, Path.Combine(fixture.Root, "items"), "relative-file.txt");

        assertions.Should().Equal(
            "File relative-file.txt is not rooted",
            "File relative-file.txt is not rooted",
            "Somehow source is outside original dir.");
    }

    public void DeployFailsTheDiagnosticAssertionForDependenciesThatAreNotRooted()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object) { DependencyFile = "testfx-mutation-dependency.dll" };

        _ = utility.RunItems([new DeploymentItem(Path.Combine(fixture.Root, "main.dll"))], Path.Combine(fixture.Root, "source.dll"), fixture.Root, fixture.Root);

        assertions.Should().Equal("File testfx-mutation-dependency.dll is not rooted");
    }

    public void DeployFailsTheDiagnosticAssertionForAFileOutsideTheDeploymentItemDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();

        _ = DeployDirectoryItem(fixture.Root, Path.Combine(fixture.Root, "items"), Path.Combine(fixture.Root, "outside", "file.txt"));

        assertions.Should().Equal("Somehow source is outside original dir.");
    }

    public void DeployDoesNotFailTheDiagnosticAssertionsForRootedFilesInsideTheDeploymentItemDirectory()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();
        string itemDirectory = Path.Combine(fixture.Root, "items");
        string file = Path.Combine(itemDirectory, "sub", "file.txt");
        string dependency = Path.Combine(fixture.Root, "dependencies", "dependency.dll");
        string? warning;

        Mock<FileUtility> directoryFiles = DeployDirectoryItem(fixture.Root, itemDirectory, file);

        directoryFiles.Verify(value => value.CopyFileOverwrite(file, Path.GetFullPath(Path.Combine(fixture.Root, "sub", "file.txt")), out warning), Times.Once);
        assertions.Should().BeEmpty();

        Mock<FileUtility> dependencyFiles = DeploymentFiles(fixture.Root);
        ExposedDeployment utility = new(dependencyFiles.Object) { DependencyFile = dependency };

        _ = utility.RunItems([new DeploymentItem(Path.Combine(fixture.Root, "main.dll"))], Path.Combine(fixture.Root, "source.dll"), fixture.Root, fixture.Root);

        dependencyFiles.Verify(value => value.CopyFileOverwrite(dependency, Path.GetFullPath(Path.Combine(fixture.Root, "dependency.dll")), out warning), Times.Once);
        assertions.Should().BeEmpty();
    }

    public void FindingFilesFailsTheDiagnosticAssertionsForMissingArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        string file = Path.Combine(fixture.Root, "file.txt");
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object);
        List<string> warnings = [];

        List<string>? missingItem = utility.FindFiles(null!, source, fixture.Root, warnings, out bool isDirectory);

        missingItem.Should().BeNull();
        isDirectory.Should().BeFalse();
        assertions.Should().Equal("deploymentItem should not be null.");
        warnings.Should().ContainSingle().Which.Should().Contain(nameof(NullReferenceException));
        assertions.Clear();
        warnings.Clear();

        utility.FindFiles(new DeploymentItem(file), null!, fixture.Root, warnings, out isDirectory).Should().Equal(file);
        utility.FindFiles(new DeploymentItem(file), string.Empty, fixture.Root, warnings, out isDirectory).Should().Equal(file);

        assertions.Should().Equal("testSourceHandler should not be null or empty.", "testSourceHandler should not be null or empty.");
        warnings.Should().BeEmpty();
    }

    public void OutputDirectoryValidationFailsTheDiagnosticAssertionsForMissingArguments()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();
        DeploymentItem item = new("source.txt");
        List<string> warnings = [];

        Action missingItem = () => _ = ExposedDeployment.IsOutputValid(null!, fixture.Root, warnings);

        missingItem.Should().Throw<NullReferenceException>();
        assertions.Should().Equal("deploymentItem should not be null.");
        assertions.Clear();

        ExposedDeployment.IsOutputValid(item, string.Empty, warnings).Should().BeFalse();
        warnings.Should().ContainSingle();
        assertions.Should().Equal("deploymentDirectory should not be null or empty.");
        assertions.Clear();
        warnings.Clear();

        ExposedDeployment.IsOutputValid(item, fixture.Root, null!).Should().BeTrue();
        assertions.Should().Equal("warnings should not be null.");
        assertions.Clear();

        ExposedDeployment.IsOutputValid(item, fixture.Root, warnings).Should().BeTrue();
        warnings.Should().BeEmpty();
        assertions.Should().BeEmpty();
    }

    public void DeployFailsTheDiagnosticAssertionWhenNoMessageLoggerIsProvided()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "source.dll");
        TestRunDirectories directories = new(fixture.Root, null, isAppDomainCreationDisabled: false);
        ExposedDeployment utility = new(DeploymentFiles(directories.OutDirectory).Object);
        UnitTestElement test = new(new TestMethod("Method", "Fixture", source, displayName: null));
        DeploymentContext context = new(fixture.Root, null);

        utility.Deploy([test], source, context, null!, directories).Should().BeFalse();

        assertions.Should().Equal("Logger should not be null");
        assertions.Clear();

        utility.Deploy([test], source, context, Mock.Of<IAdapterMessageLogger>(), directories).Should().BeFalse();

        assertions.Should().BeEmpty();
    }
#endif

    private static Mock<FileUtility> DeploymentFiles(params string[] existingDirectories)
    {
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesDirectoryExist(It.IsAny<string>())).Returns<string>(path => Array.IndexOf(existingDirectories, path) >= 0);
        files.Setup(value => value.DoesFileExist(It.IsAny<string>())).Returns(true);
        string? warning = null;
        files.Setup(value => value.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning)).Returns(string.Empty);
        return files;
    }

    private static List<string> DependencyRequestsFor(DeploymentDirectory fixture, DeploymentItemOriginType origin, string fileName)
    {
        ExposedDeployment utility = new(DeploymentFiles(fixture.Root).Object);
        DeploymentItem item = new(Path.Combine(fixture.Root, fileName), string.Empty, origin);

        _ = utility.RunItems([item], Path.Combine(fixture.Root, "source.dll"), fixture.Root, Path.Combine(fixture.Root, "results"));

        return utility.DependencyRequests;
    }

#if DEBUG && !NETFRAMEWORK
    private static Mock<FileUtility> DeployDirectoryItem(string deploymentDirectory, string itemDirectory, string fileInItemDirectory)
    {
        Mock<FileUtility> files = DeploymentFiles(deploymentDirectory, itemDirectory);
        List<string> filesInDirectory = [fileInItemDirectory];
        files
            .Setup(value => value.AddFilesFromDirectory(itemDirectory, It.IsAny<Func<string, bool>?>(), false))
            .Returns(filesInDirectory);
        ExposedDeployment utility = new(files.Object);

        _ = utility.RunItems(
            [new DeploymentItem(itemDirectory)],
            Path.Combine(deploymentDirectory, "source.dll"),
            deploymentDirectory,
            Path.Combine(deploymentDirectory, "results"));

        return files;
    }
#endif

    private sealed class ExposedDeployment(FileUtility files) : DeploymentUtilityBase(
        new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), new AssemblyUtility(), files)
    {
        public List<string> SettingSources { get; } = [];

        public List<IList<DeploymentItem>> SettingItems { get; } = [];

        public List<string> DependencyRequests { get; } = [];

        public string? SettingWarning { get; set; }

#if DEBUG && !NETFRAMEWORK
        public string? DependencyFile { get; set; }
#endif

        public static string FullPathToSource(string path, string source) => GetFullPathToDeploymentItemSource(path, source);

#if DEBUG && !NETFRAMEWORK
        public static bool IsOutputValid(DeploymentItem item, string directory, IList<string> warnings)
            => IsOutputDirectoryValid(item, directory, warnings);
#endif

        public IEnumerable<string> RunItems(IList<DeploymentItem> items, string source, string destination, string results)
            => Deploy(items, source, destination, results);

        public List<string>? FindFiles(DeploymentItem item, string source, string results, IList<string> warnings, out bool directory)
            => GetFullPathToFilesCorrespondingToDeploymentItem(item, source, results, warnings, out directory);

        public override void AddDeploymentItemsBasedOnMsTestSetting(string testSourceHandler, IList<DeploymentItem> deploymentItems, List<string> warnings)
        {
            SettingSources.Add(testSourceHandler);
            SettingItems.Add(deploymentItems);
            if (SettingWarning is not null)
            {
                warnings.Add(SettingWarning);
            }
        }

        public override string GetRootDeploymentDirectory(string baseDirectory) => Path.Combine(baseDirectory, "Deploy");

        protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
        {
            DependencyRequests.Add(deploymentItemFile);
#if DEBUG && !NETFRAMEWORK
            if (DependencyFile is not null)
            {
                filesToDeploy.Add(DependencyFile);
            }
#endif
        }
    }

    private sealed class DeploymentDirectory : IDisposable
    {
        public DeploymentDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-mutation-base-" + Guid.NewGuid().ToString("N"));

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
