// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheProviderDiscoveryMutationTests : TestContainer
{
    private const string RootAssemblyFullName = "Coverage.Root, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
    private const string LoadFailureFormat = "TypeCache: Could not load referenced assembly {0} for AssemblyFixtureProvider discovery. {1}";

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheProviderDiscoveryMutationTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    // Framework and test-infrastructure assemblies are never walked looking for providers; everything else is.
    public void FrameworkAndInfrastructureAssemblyNamesAreRecognizedByPrefixOrExactName()
    {
        MethodInfo isFrameworkAssemblyName = typeof(TypeCache).GetMethod("IsFrameworkAssemblyName", BindingFlags.NonPublic | BindingFlags.Static)!;
        (string Name, bool IsFramework)[] cases =
        [
            ("System.Runtime", true),
            ("System", true),
            ("mscorlib", true),
            ("netstandard", true),
            ("Microsoft.VisualStudio.TestPlatform.ObjectModel", true),
            ("Microsoft.VisualStudio.TestTools.UnitTesting", true),
            ("Microsoft.TestPlatform.CoreUtilities", true),
            ("Microsoft.Testing.Extensions.TrxReport", true),
            ("Microsoft.Testing.Platform", true),
            ("Microsoft.CodeAnalysis", true),
            ("Microsoft.CodeAnalysis.CSharp", true),
            ("Microsoft.Win32.Registry", true),
            ("Microsoft.NET.StringTools", true),
            ("Microsoft.CSharp", true),
            ("MSTest.TestFramework", true),
            ("MSTestAdapter.PlatformServices", true),
            ("Moq", false),
            ("Castle.Core", false),
            ("Microsoft.Extensions.Logging", false),
            ("Microsoft.Win32", false),
            ("Microsoft.CSharpExtensions", false),
            ("MSTest", false),
            ("MSTestAdapter", false),
            ("SystemExtensions", false),
            ("Mscorlib", false),
            ("system.runtime", false),
            ("microsoft.csharp", false),
            ("netstandard2", false),
        ];

        foreach ((string name, bool isFramework) in cases)
        {
            object?[] arguments = [name];

            bool actual = (bool)isFrameworkAssemblyName.Invoke(null, arguments)!;

            actual.Should().Be(isFramework, "'{0}' {1} a framework or test infrastructure assembly name", name, isFramework ? "is" : "is not");
        }
    }

    public void ReferencedAssemblyThatCannotBeLoadedIsReportedOnceAndIsNotDiscovered()
    {
        List<(string Format, object?[] Arguments)> warnings = CaptureWarnings();
        var missing = new AssemblyName("Coverage.Missing.Provider." + Guid.NewGuid().ToString("N"));
        var root = new RootAssembly(RootAssemblyFullName, missing);

        List<Assembly> discovered = EnumerateCandidateAssemblies(root);

        discovered.Should().ContainSingle().Which.Should().BeSameAs(root);
        warnings.Should().ContainSingle();
        warnings[0].Format.Should().Be(LoadFailureFormat);
        warnings[0].Arguments.Should().HaveCount(2);
        warnings[0].Arguments[0].Should().Be(missing.FullName);
        warnings[0].Arguments[1].Should().BeOfType<FileNotFoundException>();
    }

    public void ReferencedAssemblyThatCannotBeLoadedIsNotReportedWhenWarningsAreDisabled()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(false);
        var missing = new AssemblyName("Coverage.Missing.Provider." + Guid.NewGuid().ToString("N"));
        var root = new RootAssembly(RootAssemblyFullName, missing);

        List<Assembly> discovered = EnumerateCandidateAssemblies(root);

        discovered.Should().ContainSingle().Which.Should().BeSameAs(root);
        _testablePlatformServiceProvider.MockTraceLogger.Verify(value => value.Warning(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

#if NET
    public void ReferencedAssemblyWithoutMetadataPathIsReportedBeforeFallingBackToRuntimeResolution()
    {
        List<(string Format, object?[] Arguments)> infos = CaptureInfos();
        var missing = new AssemblyName("Coverage.Missing.Provider." + Guid.NewGuid().ToString("N"));
        var root = new RootAssembly(RootAssemblyFullName, missing);

        _ = EnumerateCandidateAssemblies(root);

        infos.Should().ContainSingle();
        infos[0].Format.Should().Be("TypeCache: No metadata path for referenced assembly {0}; using runtime resolution for AssemblyFixtureProvider discovery.");
        infos[0].Arguments.Should().ContainSingle().Which.Should().Be(missing.FullName);
    }

    // Moq is already loaded, so it is discovered through runtime resolution and its own references (Castle.Core) must be walked too.
    public void ReferencesOfADiscoveredAssemblyAreWalkedAsWell()
    {
        _ = new Mock<IDisposable>().Object;
        Assembly? castleCore = Array.Find(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.FullName?.StartsWith("Castle.Core,", StringComparison.Ordinal) == true);
        castleCore.Should().NotBeNull();
        var root = new RootAssembly(RootAssemblyFullName, typeof(Mock).Assembly.GetName());

        List<Assembly> discovered = EnumerateCandidateAssemblies(root);

        discovered.Should().Contain(typeof(Mock).Assembly);
        discovered.Should().Contain(castleCore!);
    }

    // An assembly without a full name is tracked as visited under the empty name, which is also the full name of an unnamed reference.
    public void UnnamedReferenceOfAnAssemblyWithoutFullNameIsNotWalked()
    {
        List<(string Format, object?[] Arguments)> infos = CaptureInfos();
        List<(string Format, object?[] Arguments)> warnings = CaptureWarnings();
        var root = new RootAssembly(null, new AssemblyName { Name = string.Empty });

        List<Assembly> discovered = EnumerateCandidateAssemblies(root);

        discovered.Should().ContainSingle().Which.Should().BeSameAs(root);
        infos.Should().BeEmpty();
        warnings.Should().BeEmpty();
    }
#endif

    private static List<Assembly> EnumerateCandidateAssemblies(Assembly assembly)
    {
        MethodInfo enumerateCandidateAssemblies = typeof(TypeCache).GetMethod("EnumerateCandidateAssemblies", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [assembly];

        return ((IEnumerable<Assembly>)enumerateCandidateAssemblies.Invoke(null, arguments)!).ToList();
    }

    private List<(string Format, object?[] Arguments)> CaptureWarnings()
    {
        List<(string Format, object?[] Arguments)> warnings = [];
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(true);
        _testablePlatformServiceProvider.MockTraceLogger.Setup(value => value.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => warnings.Add((format, arguments)));

        return warnings;
    }

#if NET
    private List<(string Format, object?[] Arguments)> CaptureInfos()
    {
        List<(string Format, object?[] Arguments)> infos = [];
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsInfoEnabled).Returns(true);
        _testablePlatformServiceProvider.MockTraceLogger.Setup(value => value.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => infos.Add((format, arguments)));

        return infos;
    }
#endif

    private sealed class RootAssembly(string? fullName, params AssemblyName[] references) : Assembly
    {
        public override string? FullName => fullName;

        public override AssemblyName[] GetReferencedAssemblies() => references;
    }
}
