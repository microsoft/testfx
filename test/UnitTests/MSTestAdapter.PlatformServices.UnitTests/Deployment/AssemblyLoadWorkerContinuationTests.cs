// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Deployment;

public sealed class AssemblyLoadWorkerContinuationTests : TestContainer
{
    public void FrameworkVersionDistinguishesMissingBadAndUnexpectedImages()
    {
        using WorkerDirectory fixture = new();
        string image = fixture.Write("image.dll");
        Mock<IAssemblyUtility> utility = new();
        AssemblyLoadWorker worker = new(utility.Object);

        worker.GetTargetFrameworkVersionStringFromPath(Path.Combine(fixture.Root, "absent.dll"), out string? missingError).Should().BeEmpty();
        missingError.Should().BeNull();
        utility.Verify(value => value.ReflectionOnlyLoadFrom(It.IsAny<string>()), Times.Never);

        utility.Setup(value => value.ReflectionOnlyLoadFrom(image)).Throws(new BadImageFormatException("not a manifest"));
        worker.GetTargetFrameworkVersionStringFromPath(image, out string? badImageError).Should().BeEmpty();
        badImageError.Should().Be("AssemblyHelper:GetTargetFrameworkVersionString() caught BadImageFormatException. Falling to native binary.");

        InvalidOperationException failure = new("metadata reader unavailable");
        utility.Setup(value => value.ReflectionOnlyLoadFrom(image)).Throws(failure);
        worker.GetTargetFrameworkVersionStringFromPath(image, out string? unexpectedError).Should().BeEmpty();
        unexpectedError.Should().Be($"AssemblyHelper:GetTargetFrameworkVersionString() Returning default. Unhandled exception: {failure}.");
        utility.Verify(value => value.ReflectionOnlyLoadFrom(image), Times.Exactly(2));
    }

    public void FrameworkVersionReadsTheActualTargetFrameworkAttribute()
    {
        Mock<IAssemblyUtility> utility = new();
        Assembly assembly = typeof(AssemblyLoadWorkerContinuationTests).Assembly;
        utility.Setup(value => value.ReflectionOnlyLoadFrom(assembly.Location)).Returns(assembly);
        AssemblyLoadWorker worker = new(utility.Object);

        string version = worker.GetTargetFrameworkVersionStringFromPath(assembly.Location, out string? error);

#if NET462
        version.Should().Be(".NETFramework,Version=v4.6.2");
#else
        version.Should().Be(".NETFramework,Version=v4.8");
#endif
        error.Should().BeNull();
        utility.Verify(value => value.ReflectionOnlyLoadFrom(assembly.Location), Times.Once);
    }

    public void DependencyWalkDeduplicatesCyclesAndDeploysOnlyExistingNonMainModules()
    {
        using WorkerDirectory fixture = new();
        string rootPath = fixture.Write("GraphRoot.dll");
        string dependencyPath = fixture.Write("GraphDependency.dll");
        string part = fixture.Write("part.netmodule");
        string extensionless = fixture.Write("extensionless");
        string absent = Path.Combine(fixture.Root, "missing.netmodule");
        GraphAssembly root = new("GraphRoot", rootPath);
        GraphAssembly dependency = new("GraphDependency", dependencyPath);
        root.References = [dependency.GetName(), dependency.GetName()];
        dependency.References = [root.GetName(), dependency.GetName()];
        root.ModulesToReturn =
        [
            Module("GraphRoot.dll", rootPath),
            Module("part.netmodule", part),
            Module("part.netmodule", part),
            Module("missing.netmodule", absent),
            Module("extensionless", extensionless),
        ];
        Mock<IAssemblyUtility> utility = new();
        utility.Setup(value => value.ReflectionOnlyLoadFrom(rootPath)).Returns(root);
        utility.Setup(value => value.ReflectionOnlyLoad(dependency.FullName)).Returns(dependency);
        AssemblyLoadWorker worker = new(utility.Object);

        IReadOnlyCollection<string> files = worker.GetFullPathToDependentAssemblies(rootPath, out IList<string> warnings);

        files.Should().Equal(dependencyPath, part, extensionless);
        warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependencyWithoutReason, absent));
        utility.Verify(value => value.ReflectionOnlyLoad(dependency.FullName), Times.Once);
        utility.Verify(value => value.ReflectionOnlyLoad(root.FullName), Times.Never);
    }

    public void MissingModuleCollectionReportsExactFileAndReason()
    {
        using WorkerDirectory fixture = new();
        string image = fixture.Write("GraphRoot.dll");
        FileNotFoundException missing = new("the module manifest is absent", "part.netmodule");
        GraphAssembly root = new("GraphRoot", image) { ModuleFailure = missing };
        Mock<IAssemblyUtility> utility = new();
        utility.Setup(value => value.ReflectionOnlyLoadFrom(image)).Returns(root);
        AssemblyLoadWorker worker = new(utility.Object);

        worker.GetFullPathToDependentAssemblies(image, out IList<string> warnings).Should().BeEmpty();

        warnings.Should().Equal(string.Format(CultureInfo.CurrentCulture, Resource.MissingDeploymentDependency, missing.FileName, missing.Message));
    }

    private static Module Module(string name, string path) => new GraphModule(name, path);

    private sealed class GraphModule(string name, string path) : Module
    {
        public override string Name => name;

        public override string FullyQualifiedName => path;
    }

    private sealed class GraphAssembly(string name, string path) : Assembly
    {
        public AssemblyName[] References { get; set; } = [];

        public Module[] ModulesToReturn { get; set; } = [];

        public FileNotFoundException? ModuleFailure { get; init; }

        public override string FullName => new AssemblyName(name).FullName;

        public override string Location => path;

        public override AssemblyName GetName() => new(name);

        public override AssemblyName[] GetReferencedAssemblies() => References;

        public override Module[] GetModules(bool getResourceModules) => ModuleFailure is null ? ModulesToReturn : throw ModuleFailure;
    }

    private sealed class WorkerDirectory : IDisposable
    {
        public WorkerDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-ad03-worker-" + Guid.NewGuid().ToString("N"));

        public string Write(string name)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, "fixture module");
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
