// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Reflection.Emit;

using AwesomeAssertions;

#if DEBUG
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Utilities;

public sealed class AssemblyUtilityContinuationTests : TestContainer
{
    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();

    public AssemblyUtilityContinuationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsErrorEnabled).Returns(true);
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void DependencyWalkReturnsOnlyTheCopiedLocalReferenceWithTracingEnabled()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-dependencies-" + Guid.NewGuid().ToString("N"));
        string images = Directory.CreateDirectory(Path.Combine(root, "images")).FullName;
        string application = Directory.CreateDirectory(Path.Combine(root, "application")).FullName;
        try
        {
            string dependencyName = "Ad03Local" + Guid.NewGuid().ToString("N");
            Type dependencyType = SaveAssembly(images, dependencyName, typeof(object));
            string dependency = Path.Combine(application, dependencyName + ".dll");
            File.Copy(Path.Combine(images, dependencyName + ".dll"), dependency);
            string rootName = "Ad03Graph" + Guid.NewGuid().ToString("N");
            SaveAssembly(application, rootName, dependencyType);
            string image = Path.Combine(application, rootName + ".dll");

            IReadOnlyList<string> dependencies = new AssemblyUtility().GetFullPathToDependentAssemblies(image, null, out IList<string> warnings);

            dependencies.Should().Equal(dependency);
            warnings.Should().BeEmpty();
            _provider.MockTraceLogger.Verify(logger => logger.Info("AssemblyDependencyFinder.GetDependentAssemblies: Created AppDomain."), Times.Once);
            _provider.MockTraceLogger.Verify(logger => logger.Info("AssemblyDependencyFinder.GetDependentAssemblies: loaded the worker."), Times.Once);
            _provider.MockTraceLogger.Verify(logger => logger.Info("AssemblyDependencyFinder.GetDependentAssemblies: unloading AppDomain succeeded."), Times.Once);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void RejectedMainImagesAndSatelliteImagesReportTheExactPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-satellites-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string nonAssembly = Path.Combine(root, "data.txt");
            File.WriteAllText(nonAssembly, "plain data");
#if DEBUG
            var assertions = new List<string?>();
            using var observer = new DiagnosticAssertionObserver(assertions.Add);
#endif
            AssemblyUtility utility = new();
            utility.GetSatelliteAssemblies(nonAssembly).Should().BeEmpty();
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("AssemblyUtilities.GetSatelliteAssemblies: the specified file '{0}' is not managed assembly.", nonAssembly),
                Times.Once);

            string managed = Path.Combine(root, "sample.dll");
            File.Copy(typeof(AssemblyUtility).Assembly.Location, managed);
            string culture = Directory.CreateDirectory(Path.Combine(root, "fr")).FullName;
            string invalidSatellite = Path.Combine(culture, "sample.resources.dll");
            File.WriteAllText(invalidSatellite, "invalid satellite image");
            utility.GetSatelliteAssemblies(managed).Should().BeEmpty();
            _provider.MockTraceLogger.Verify(
                logger => logger.Error("AssemblyUtilities.GetSatelliteAssemblies: found assembly '{0}' installed as satellite but it's not managed assembly.", invalidSatellite),
                Times.Once);
#if DEBUG
            assertions.Should().Equal($"AssemblyUtilities.GetSatelliteAssemblies: the file '{nonAssembly}' is not an assembly.");
#endif
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    public void LockedManagedImageIsRejectedUntilItsReadAccessIsReleased()
    {
        string root = Path.Combine(Path.GetTempPath(), "testfx-ad03-image-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string image = Path.Combine(root, "managed.dll");
            File.Copy(typeof(AssemblyUtility).Assembly.Location, image);
            using (FileStream exclusive = File.Open(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                AssemblyUtility.IsAssembly(image).Should().BeFalse("the metadata reader cannot open an exclusively owned file");
            }

            AssemblyUtility.IsAssembly(image).Should().BeTrue("the valid image is readable after the owned handle is disposed");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Type SaveAssembly(string directory, string name, Type reference)
    {
        AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Save, directory);
        ModuleBuilder module = assembly.DefineDynamicModule(name, name + ".dll");
        TypeBuilder type = module.DefineType("Anchor", TypeAttributes.Public);
        type.DefineField("Reference", reference, FieldAttributes.Public);
        Type created = type.CreateType();
        assembly.Save(name + ".dll");
        return created;
    }
}
#endif
