// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using AwesomeAssertions;

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

public sealed class DeploymentUtilityContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public DeploymentUtilityContinuationTests()
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

    public void ConfigPreferenceAndMissingFilesPreserveExactDeploymentItems()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "tests.dll");
        string assemblyConfig = source + ".config";
        string applicationConfig = Path.Combine(fixture.Root, "App.Config");
        HashSet<string> existing = [assemblyConfig, applicationConfig];
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesFileExist(It.IsAny<string>())).Returns<string>(existing.Contains);
        ExposedDeployment utility = new(files.Object);
        List<DeploymentItem> items = [];

        utility.AddConfig(source, items).Should().Be(assemblyConfig);
        items.Select(item => item.SourcePath).Should().Equal(assemblyConfig);
        existing.Remove(assemblyConfig);
        items.Clear();
        utility.AddConfig(source, items).Should().Be(applicationConfig);
        items.Select(item => item.SourcePath).Should().Equal(applicationConfig);
        existing.Clear();
        items.Clear();
        utility.AddConfig(source, items).Should().BeNull();
        items.Should().BeEmpty();
    }

    public void MissingItemsUseBasenameFallbackAndReportTheFinalMissingPath()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "tests.dll");
        string fallback = Path.Combine(fixture.Root, "rows.csv");
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesFileExist(fallback)).Returns(true);
        ExposedDeployment utility = new(files.Object);
        DeploymentItem item = new(Path.Combine("absent", "nested", "rows.csv"));
        List<string> warnings = [];

        utility.FindFiles(item, source, fixture.Root, warnings, out bool directory).Should().Equal(fallback);
        directory.Should().BeFalse();
        warnings.Should().BeEmpty();
        files.Setup(value => value.DoesFileExist(fallback)).Returns(false);
        utility.FindFiles(item, source, fixture.Root, warnings, out directory).Should().BeNull();
        directory.Should().BeFalse();
        warnings.Should().Equal(MissingItemWarning(item, fallback));
    }

    public void InvalidOutputDirectoriesAreRejectedBeforeCopy()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "tests.dll");
        string destination = Path.Combine(fixture.Root, "out");
        Mock<FileUtility> files = DeploymentFiles(destination);
        ExposedDeployment utility = new(files.Object);
        DeploymentItem traversal = new("data.csv", Path.Combine("..", "escape"));
        DeploymentItem invalid = new("other.csv", "\0");
        ArgumentException invalidPath;
        try
        {
            _ = Path.GetFullPath(Path.Combine(destination, invalid.RelativeOutputDirectory));
            throw new InvalidOperationException("The fixture must exercise the OS path validator.");
        }
        catch (ArgumentException exception)
        {
            invalidPath = exception;
        }

        utility.RunItems([traversal, invalid], source, destination, fixture.Root).Should().Equal(
            string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorBadDeploymentItem, "data.csv", traversal.RelativeOutputDirectory),
            string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToAccesOutputDirectory, "other.csv", destination, invalidPath.GetType(), invalidPath.Message));
        string? warning;
        files.Verify(value => value.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning), Times.Never);
    }

    public void DeploymentSkipsSelfAndSamePathAndDoesNotOverwriteConflictingOrigins()
    {
        using DeploymentDirectory fixture = new();
        string destination = Path.Combine(fixture.Root, "out");
        string source = Path.Combine(fixture.Root, "tests.dll");
        string first = Path.Combine(fixture.Root, "first", "shared.txt");
        string second = Path.Combine(fixture.Root, "second", "shared.txt");
        string samePath = Path.Combine(destination, "existing.txt");
        string self = Path.Combine(fixture.Root, Path.GetFileName(typeof(ExposedDeployment).Assembly.Location));
        Mock<FileUtility> files = DeploymentFiles(destination);
        string? warning = null;
        files.Setup(value => value.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning))
            .Returns((string from, string to, string? _) => to);
        ExposedDeployment utility = new(files.Object);

        utility.RunItems(
            [new(self), new(samePath), new(first), new(second), new(first.ToUpperInvariant())],
            source, destination, fixture.Root).Should().BeEmpty();

        string copied = Path.Combine(destination, "shared.txt");
        files.Verify(value => value.CopyFileOverwrite(first, copied, out warning), Times.Once);
        files.Verify(value => value.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out warning), Times.Once);
        files.Verify(value => value.SetAttributes(copied, FileAttributes.Normal), Times.Once);
        _provider.MockTraceLogger.Verify(
            logger => logger.Warning("Conflict during copying file: '{0}' and '{1}' are from different origins although they might be the same.", second, first),
            Times.Once);
    }

    public void CopyFailuresDoNotStopLaterItemsOrSetAttributesOnFailedCopies()
    {
        using DeploymentDirectory fixture = new();
        string destination = Path.Combine(fixture.Root, "out");
        string source = Path.Combine(fixture.Root, "tests.dll");
        string failed = Path.Combine(fixture.Root, "failed.txt");
        string good = Path.Combine(fixture.Root, "good.txt");
        string failedDestination = Path.Combine(destination, "failed.txt");
        string goodDestination = Path.Combine(destination, "good.txt");
        Mock<FileUtility> files = DeploymentFiles(destination);
        string? failedWarning = "copy was rejected by the file collaborator";
        string? noWarning = null;
        files.Setup(value => value.CopyFileOverwrite(failed, failedDestination, out failedWarning)).Returns(string.Empty);
        files.Setup(value => value.CopyFileOverwrite(good, goodDestination, out noWarning)).Returns(goodDestination);
        ExposedDeployment utility = new(files.Object);

        utility.RunItems([new(failed), new(good)], source, destination, fixture.Root).Should().Equal(failedWarning);

        files.Verify(value => value.CopyFileOverwrite(good, goodDestination, out noWarning), Times.Once);
        files.Verify(value => value.SetAttributes(failedDestination, It.IsAny<FileAttributes>()), Times.Never);
        files.Verify(value => value.SetAttributes(goodDestination, FileAttributes.Normal), Times.Once);
    }

#if !NETFRAMEWORK
    public void InvalidDestinationDoesNotPreventLaterDeployment()
    {
        using DeploymentDirectory fixture = new();
        string destination = Path.Combine(fixture.Root, "out");
        string source = Path.Combine(fixture.Root, "tests.dll");
        string invalid = Path.Combine(fixture.Root, "\0invalid.txt");
        string good = Path.Combine(fixture.Root, "good.txt");
        string goodDestination = Path.Combine(destination, "good.txt");
        Mock<FileUtility> files = DeploymentFiles(destination);
        string? warning = null;
        files.Setup(value => value.CopyFileOverwrite(good, goodDestination, out warning)).Returns(goodDestination);
        ExposedDeployment utility = new(files.Object);

        string[] warnings = utility.RunItems([new(invalid), new(good)], source, destination, fixture.Root).ToArray();

        warnings.Should().ContainSingle().Which.Should().Contain(nameof(ArgumentException)).And.Contain("invalid.txt");
        files.Verify(value => value.CopyFileOverwrite(good, goodDestination, out warning), Times.Once);
        files.Verify(value => value.SetAttributes(goodDestination, FileAttributes.Normal), Times.Once);
    }
#endif

    public void PublicDeploymentLogsWarningsAndReflectsRequestedItems()
    {
        using DeploymentDirectory fixture = new();
        string source = Path.Combine(fixture.Root, "tests.dll");
        TestRunDirectories directories = new(fixture.Root, null, isAppDomainCreationDisabled: false);
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesDirectoryExist(directories.OutDirectory)).Returns(true);
        files.Setup(value => value.DoesFileExist(source)).Returns(true);
        ExposedDeployment utility = new(files.Object);
        Mock<IAdapterMessageLogger> logger = new();
        UnitTestElement test = new(new TestMethod("Method", "Fixture", source, displayName: null))
        {
            DeploymentItems = [new KeyValuePair<string, string>("missing.csv", string.Empty)],
        };

        utility.Deploy([test], source, new DeploymentContext(fixture.Root, null), logger.Object, directories).Should().BeTrue();

        string expected = MissingItemWarning(new DeploymentItem("missing.csv"), Path.Combine(fixture.Root, "missing.csv"));
        logger.Verify(value => value.SendMessage(MessageLevel.Warning, expected), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("MSTestExecutor: Found that deployment items for source {0} are: ", source), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Info("MSTestExecutor: SourcePath: - {0}", "missing.csv"), Times.Once);
        _provider.MockTraceLogger.Verify(
            logger => logger.Info("MSTestExecutor: Using deployment directory {0} for source {1}.", directories.OutDirectory, source),
            Times.Once);
        test.DeploymentItems = null;
        utility.Deploy([test], source, new DeploymentContext(fixture.Root, null), logger.Object, directories).Should().BeFalse();
    }

    public void DirectoryDeploymentSkipsResultsAndPreservesRelativeTree()
    {
        using DeploymentDirectory fixture = new();
        string source = fixture.Write("host.txt", "host");
        string input = Directory.CreateDirectory(Path.Combine(fixture.Root, "input")).FullName;
        fixture.Write(Path.Combine("input", "first.txt"), "first");
        fixture.Write(Path.Combine("input", "sub", "second.txt"), "second");
        fixture.Write(Path.Combine("input", "results", "run.trx"), "must not deploy");
        string results = Path.Combine(input, "results");
        string destination = Directory.CreateDirectory(Path.Combine(fixture.Root, "out")).FullName;
        ExposedDeployment utility = new(new FileUtility());

        utility.RunItems([new DeploymentItem(input)], source, destination, results).Should().BeEmpty();

        string first = Path.Combine(destination, "first.txt");
        string second = Path.Combine(destination, "sub", "second.txt");
        Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Should().BeEquivalentTo([first, second]);
        File.ReadAllText(first).Should().Be("first");
        File.ReadAllText(second).Should().Be("second");
        File.ReadAllText(Path.Combine(results, "run.trx")).Should().Be("must not deploy");
    }

#if NETFRAMEWORK
    public void PerTestAssemblyDependenciesAreCopiedAndTheirWarningsAreForwarded()
    {
        using DeploymentDirectory fixture = new();
        bool previous = MSTestSettingsProvider.Settings.DeployTestSourceDependencies;
        try
        {
            SetDeployDependencies(true);
            string source = typeof(DeploymentUtilityContinuationTests).Assembly.Location;
            string item = typeof(TestMethodAttribute).Assembly.Location;
            string dependency = typeof(XmlDocument).Assembly.Location;
            TestRunDirectories directories = new(fixture.Root, null, isAppDomainCreationDisabled: false);
            Mock<FileUtility> files = DeploymentFiles(directories.OutDirectory);
            files.Setup(value => value.DoesFileExist(It.IsAny<string>())).Returns<string>(path => !path.EndsWith(".config", StringComparison.OrdinalIgnoreCase));
            string? copyWarning = null;
            files.Setup(value => value.CopyFileOverwrite(It.IsAny<string>(), It.IsAny<string>(), out copyWarning))
                .Returns((string from, string to, string? _) => to);
            Mock<AssemblyUtility> assemblies = new();
            IList<string> storageWarnings = [];
            IList<string> itemWarnings = ["per-test reference warning"];
            assemblies.Setup(value => value.GetFullPathToDependentAssemblies(source, null, out storageWarnings)).Returns([]);
            assemblies.Setup(value => value.GetFullPathToDependentAssemblies(item, null, out itemWarnings)).Returns([dependency]);
            assemblies.Setup(value => value.GetSatelliteAssemblies(It.IsAny<string>())).Returns([]);
            DeploymentUtility utility = new(new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), assemblies.Object, files.Object);
            Mock<IAdapterMessageLogger> logger = new();
            UnitTestElement test = new(new TestMethod("Method", "Fixture", source, displayName: null))
            {
                DeploymentItems = [new KeyValuePair<string, string>(item, string.Empty)],
            };

            utility.Deploy([test], source, new DeploymentContext(fixture.Root, null), logger.Object, directories).Should().BeTrue();

            string destination = Path.Combine(directories.OutDirectory, Path.GetFileName(dependency));
            files.Verify(value => value.CopyFileOverwrite(dependency, destination, out copyWarning), Times.Once);
            files.Verify(value => value.SetAttributes(destination, FileAttributes.Normal), Times.Once);
            logger.Verify(value => value.SendMessage(MessageLevel.Warning, "per-test reference warning"), Times.Once);
        }
        finally
        {
            SetDeployDependencies(previous);
        }
    }

    public void DisabledDependencyDeploymentAddsOnlyTheSourceDirectory()
    {
        bool previous = MSTestSettingsProvider.Settings.DeployTestSourceDependencies;
        try
        {
            SetDeployDependencies(false);
            List<DeploymentItem> items = [];
            List<string> warnings = [];
            string source = typeof(DeploymentUtilityContinuationTests).Assembly.Location;

            new DeploymentUtility().AddDeploymentItemsBasedOnMsTestSetting(source, items, warnings);

            items.Select(item => item.SourcePath).Should().Equal(Path.GetDirectoryName(source));
            items[0].OriginType.Should().Be(DeploymentItemOriginType.PerTestDeployment);
            warnings.Should().BeEmpty();
        }
        finally
        {
            SetDeployDependencies(previous);
        }
    }

    public void InvalidStorageAndDependencyFailuresRetainExactWarnings()
    {
        Mock<FileUtility> files = new();
        Mock<AssemblyUtility> assemblies = new();
        DeploymentUtility utility = new(new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), assemblies.Object, files.Object);
        List<DeploymentItem> items = [];
        List<string> warnings = [];

        utility.ProcessNewStorage(string.Empty, items, warnings);

        items.Should().BeEmpty();
        warnings.Should().Equal(Resource.DeploymentItemPathCannotBeNullOrEmpty);
        warnings.Clear();
        string source = typeof(DeploymentUtility).Assembly.Location;
        IList<string> dependencyWarnings = [];
        IOException failure = new("dependency access rejected");
        assemblies.Setup(value => value.GetFullPathToDependentAssemblies(source, null, out dependencyWarnings)).Throws(failure);

        utility.ProcessNewStorage(source, items, warnings);

        items.Select(item => item.SourcePath).Should().Equal(source);
        items[0].OriginType.Should().Be(DeploymentItemOriginType.TestStorage);
        warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorFailedToDeployDependencies, source, failure));
    }

    public void DependencyAndSatelliteItemsRetainOriginsPathsAndWarnings()
    {
        bool previous = MSTestSettingsProvider.Settings.DeployTestSourceDependencies;
        try
        {
            SetDeployDependencies(true);
            string source = typeof(DeploymentUtility).Assembly.Location;
            string dependency = typeof(TestMethodAttribute).Assembly.Location;
            string satellite = Path.Combine(Path.GetDirectoryName(source), "fr", "source.resources.dll");
            Mock<FileUtility> files = new();
            files.Setup(value => value.DoesFileExist(It.IsAny<string>())).Returns<string>(path => path == source || path == dependency);
            Mock<AssemblyUtility> assemblies = new();
            IList<string> dependencyWarnings = ["unresolved optional reference"];
            assemblies.Setup(value => value.GetFullPathToDependentAssemblies(source, null, out dependencyWarnings)).Returns([dependency]);
            assemblies.Setup(value => value.GetSatelliteAssemblies(source)).Returns([satellite]);
            assemblies.Setup(value => value.GetSatelliteAssemblies(dependency)).Returns([]);
            DeploymentUtility utility = new(new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), assemblies.Object, files.Object);
            List<DeploymentItem> items = [];
            List<string> warnings = [];

            utility.AddDeploymentItemsBasedOnMsTestSetting(source, items, warnings);

            items.Select(item => item.SourcePath).Should().Equal(source, dependency, satellite);
            items.Select(item => item.OriginType).Should().Equal(DeploymentItemOriginType.TestStorage, DeploymentItemOriginType.Dependency, DeploymentItemOriginType.Satellite);
            items[2].RelativeOutputDirectory.Should().Be("fr");
            warnings.Should().Equal("unresolved optional reference");
            _provider.MockTraceLogger.Verify(logger => logger.Info("DeploymentManager: Reference:{0} ", dependency), Times.Once);
        }
        finally
        {
            SetDeployDependencies(previous);
        }
    }

    public void SatelliteFailuresAreReportedAndOtherItemsContinue()
    {
        string source = typeof(DeploymentUtility).Assembly.Location;
        string other = typeof(TestMethodAttribute).Assembly.Location;
        string satellite = Path.Combine(Path.GetDirectoryName(other), "de", "other.resources.dll");
        IOException inspectionFailure = new("file inspection denied");
        IOException satelliteFailure = new("satellite directory denied");
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesFileExist(source)).Throws(inspectionFailure);
        files.Setup(value => value.DoesFileExist(other)).Returns(true);
        Mock<AssemblyUtility> assemblies = new();
        assemblies.Setup(value => value.GetSatelliteAssemblies(source)).Throws(satelliteFailure);
        assemblies.Setup(value => value.GetSatelliteAssemblies(other)).Returns([satellite]);
        DeploymentUtility utility = new(new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), assemblies.Object, files.Object);
        DeploymentItem first = new(source, "first");
        List<string> warnings = [];

        DeploymentItem[] result = utility.GetSatellites([first, new DeploymentItem(other, "assets")], source, warnings).ToArray();

        result.Select(item => item.SourcePath).Should().Equal(satellite);
        result[0].RelativeOutputDirectory.Should().Be(Path.Combine("assets", "de"));
        result[0].OriginType.Should().Be(DeploymentItemOriginType.Satellite);
        warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.DeploymentErrorGettingSatellite, first, satelliteFailure.GetType(), satelliteFailure.Message));
        _provider.MockTraceLogger.Verify(logger => logger.Warning("DeploymentManager.GetSatellites: {0}", inspectionFailure), Times.Once);
        _provider.MockTraceLogger.Verify(logger => logger.Warning("DeploymentManager.GetSatellites: {0}", satelliteFailure), Times.Once);
    }

    private static void SetDeployDependencies(bool value)
    {
        using StringReader text = new($"<MSTest><DeployTestSourceDependencies>{value}</DeployTestSourceDependencies></MSTest>");
        using var reader = XmlReader.Create(text);
        reader.MoveToContent();
        MSTestAdapterSettings.ToSettings(reader);
    }
#endif

    private static Mock<FileUtility> DeploymentFiles(string destination)
    {
        Mock<FileUtility> files = new();
        files.Setup(value => value.DoesDirectoryExist(It.IsAny<string>())).Returns<string>(path => path == destination);
        files.Setup(value => value.DoesFileExist(It.IsAny<string>())).Returns(true);
        return files;
    }

    private static string MissingItemWarning(DeploymentItem item, string path) => string.Format(
        CultureInfo.CurrentCulture,
        Resource.DeploymentErrorFailedToGetFileForDeploymentItem,
        item,
        typeof(FileNotFoundException),
        string.Format(CultureInfo.CurrentCulture, Resource.CannotFindFile, path));

    private sealed class ExposedDeployment(FileUtility files) : DeploymentUtilityBase(
        new DeploymentItemUtility(Mock.Of<IReflectionOperations>()), new AssemblyUtility(), files)
    {
        public IEnumerable<string> RunItems(IList<DeploymentItem> items, string source, string destination, string results)
            => Deploy(items, source, destination, results);

        public List<string>? FindFiles(DeploymentItem item, string source, string results, IList<string> warnings, out bool directory)
            => GetFullPathToFilesCorrespondingToDeploymentItem(item, source, results, warnings, out directory);

        public string? AddConfig(string source, IList<DeploymentItem> items) => AddTestSourceConfigFileIfExists(source, items);

        public override void AddDeploymentItemsBasedOnMsTestSetting(string testSourceHandler, IList<DeploymentItem> deploymentItems, List<string> warnings)
        {
        }

        public override string GetRootDeploymentDirectory(string baseDirectory) => Path.Combine(baseDirectory, "Deploy");

        protected override void AddDependenciesOfDeploymentItem(string deploymentItemFile, IList<string> filesToDeploy, IList<string> warnings)
        {
        }
    }

    private sealed class DeploymentDirectory : IDisposable
    {
        public DeploymentDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-deployment-" + Guid.NewGuid().ToString("N"));

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
