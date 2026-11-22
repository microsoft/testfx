// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
#if NET
using System.Runtime.Loader;
#endif

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheProviderDiscoveryMetadataMutationTests : TestContainer
{
    private const string MarkerNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";
    private const string MarkerName = "AssemblyFixtureProviderAttribute";
    private const string OtherNamespace = "Coverage.OtherNamespace";
    private const string ReadFailureFormat = "TypeCache: Could not read assembly metadata from {0}; using runtime resolution for AssemblyFixtureProvider discovery. {1}";

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheProviderDiscoveryMetadataMutationTests()
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

    public void ProviderMarkerReferencedThroughATypeReferenceIsDetectedAndAssemblyReferencesAreReturned()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteAssembly(
            "Coverage.TypeReferenceMarker",
            ["Coverage.Dependency.One", "Coverage.Dependency.Two"],
            [(false, MarkerNamespace, MarkerName)]);

        (bool result, bool hasMarker, AssemblyName[] references) = TryReadProviderMetadata(path);

        result.Should().BeTrue();
        hasMarker.Should().BeTrue();
        references.Select(reference => reference.Name).Should().Equal(
            "Coverage.Dependency.One",
            "Coverage.Dependency.Two",
            "Microsoft.VisualStudio.TestPlatform.TestFramework");
    }

    public void ProviderMarkerDeclaredInTheAssemblyItselfIsDetected()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteAssembly("Coverage.TypeDefinitionMarker", [], [(true, MarkerNamespace, MarkerName)]);

        (bool result, bool hasMarker, AssemblyName[] references) = TryReadProviderMetadata(path);

        result.Should().BeTrue();
        hasMarker.Should().BeTrue();
        references.Should().BeEmpty();
    }

    public void AttributesDeclaredInTheAssemblyThatOnlyPartiallyMatchTheProviderMarkerAreNotDetected()
    {
        using var directory = new TemporaryDirectory();
        string sameNameOtherNamespace = directory.WriteAssembly("Coverage.SameNameMarker", [], [(true, OtherNamespace, MarkerName)]);
        string sameNamespaceOtherName = directory.WriteAssembly("Coverage.SameNamespaceMarker", [], [(true, MarkerNamespace, "OtherAttribute")]);

        (bool resultForName, bool hasMarkerForName, _) = TryReadProviderMetadata(sameNameOtherNamespace);
        (bool resultForNamespace, bool hasMarkerForNamespace, _) = TryReadProviderMetadata(sameNamespaceOtherName);

        resultForName.Should().BeTrue();
        hasMarkerForName.Should().BeFalse("only the attribute name matches the provider marker");
        resultForNamespace.Should().BeTrue();
        hasMarkerForNamespace.Should().BeFalse("only the attribute namespace matches the provider marker");
    }

    public void ProviderMarkerIsStillDetectedWhenOtherAttributesFollowIt()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteAssembly(
            "Coverage.MarkerBetweenOtherAttributes",
            [],
            [
                (false, OtherNamespace, "FirstAttribute"),
                (false, MarkerNamespace, MarkerName),
                (false, OtherNamespace, "SecondAttribute"),
                (false, OtherNamespace, "ThirdAttribute"),
            ]);

        (bool result, bool hasMarker, _) = TryReadProviderMetadata(path);

        result.Should().BeTrue();
        hasMarker.Should().BeTrue();
    }

    public void UnreadableAssembliesAreReportedAsNotReadWithoutMarkerOrReferences()
    {
        using var directory = new TemporaryDirectory();
        string missing = Path.Combine(directory.DirectoryPath, "Missing.dll");
        string notAnAssembly = Path.Combine(directory.DirectoryPath, "NotAnAssembly.dll");
        File.WriteAllText(notAnAssembly, "This is not a portable executable.");

        (bool resultForMissing, bool hasMarkerForMissing, AssemblyName[] referencesForMissing) = TryReadProviderMetadata(missing);
        (bool resultForInvalid, bool hasMarkerForInvalid, AssemblyName[] referencesForInvalid) = TryReadProviderMetadata(notAnAssembly);

        resultForMissing.Should().BeFalse("a missing file cannot be read");
        hasMarkerForMissing.Should().BeFalse();
        referencesForMissing.Should().BeEmpty();
        resultForInvalid.Should().BeFalse("a file that is not a managed assembly cannot be read");
        hasMarkerForInvalid.Should().BeFalse();
        referencesForInvalid.Should().BeEmpty();
    }

    public void UnreadableAssemblyIsReportedAsAWarningWhenWarningsAreEnabled()
    {
        List<(string Format, object?[] Arguments)> warnings = CaptureWarnings();
        using var directory = new TemporaryDirectory();
        string missing = Path.Combine(directory.DirectoryPath, "Missing.dll");

        _ = TryReadProviderMetadata(missing);

        warnings.Should().ContainSingle();
        warnings[0].Format.Should().Be(ReadFailureFormat);
        warnings[0].Arguments.Should().HaveCount(2);
        warnings[0].Arguments[0].Should().Be(missing);
        warnings[0].Arguments[1].Should().BeOfType<FileNotFoundException>();
    }

    public void UnreadableAssemblyIsNotReportedWhenWarningsAreDisabled()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(false);
        using var directory = new TemporaryDirectory();

        (bool result, _, _) = TryReadProviderMetadata(Path.Combine(directory.DirectoryPath, "Missing.dll"));

        result.Should().BeFalse();
        _testablePlatformServiceProvider.MockTraceLogger.Verify(value => value.Warning(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

#if NET
    public void TrustedPlatformAssemblyThatCannotBeInspectedIsReportedAndNotResolved()
    {
        List<(string Format, object?[] Arguments)> warnings = CaptureWarnings();
        string name = "Coverage.Stale." + Guid.NewGuid().ToString("N");
        using var trustedAssemblies = new TrustedPlatformAssembliesOverride(Path.Combine(Path.GetTempPath(), name + ".dll"));
        var referenceName = new AssemblyName(name);

        string? path = Resolve(CreateResolver(), new RootAssembly(), referenceName);

        path.Should().BeNull();
        warnings.Should().ContainSingle();
        warnings[0].Format.Should().Be("TypeCache: Could not resolve a metadata path for {0}; using runtime resolution for AssemblyFixtureProvider discovery. {1}");
        warnings[0].Arguments.Should().HaveCount(2);
        warnings[0].Arguments[0].Should().Be(referenceName.FullName);
        warnings[0].Arguments[1].Should().BeAssignableTo<IOException>();
    }

    public void TrustedPlatformAssemblyThatCannotBeInspectedIsNotReportedWhenWarningsAreDisabled()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsWarningEnabled).Returns(false);
        string name = "Coverage.Stale." + Guid.NewGuid().ToString("N");
        using var trustedAssemblies = new TrustedPlatformAssembliesOverride(Path.Combine(Path.GetTempPath(), name + ".dll"));

        string? path = Resolve(CreateResolver(), new RootAssembly(), new AssemblyName(name));

        path.Should().BeNull();
        _testablePlatformServiceProvider.MockTraceLogger.Verify(value => value.Warning(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

    public void EmptyTrustedPlatformAssemblyEntriesAreIgnored()
    {
        string separator = Path.PathSeparator.ToString();
        string first = Path.Combine(Path.GetTempPath(), "Coverage.First.dll");
        string second = Path.Combine(Path.GetTempPath(), "Coverage.Second.dll");
        using var trustedAssemblies = new TrustedPlatformAssembliesOverride(first + separator + separator + second + separator);
        MethodInfo readTrustedAssemblyPaths = CreateResolver().GetType().GetMethod("ReadTrustedAssemblyPaths", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [];

        var paths = (Dictionary<string, string?>)readTrustedAssemblyPaths.Invoke(null, arguments)!;

        paths.Should().HaveCount(2);
        paths.Should().ContainKey("Coverage.First").WhoseValue.Should().Be(first);
        paths.Should().ContainKey("Coverage.Second").WhoseValue.Should().Be(second);
    }

    // An assembly loaded in a custom load context binds through that context, which the default context's trusted platform assemblies do not describe.
    public void ReferrerLoadedInACustomLoadContextIsNotResolvedThroughTheTrustedPlatformAssemblies()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteAssembly("Coverage.CustomContext." + Guid.NewGuid().ToString("N"), [], []);
        using var trustedAssemblies = new TrustedPlatformAssembliesOverride(path);
        var referenceName = AssemblyName.GetAssemblyName(path);
        object resolver = CreateResolver();
        var context = new CollectibleLoadContext();
        try
        {
            Assembly customContextReferrer = context.LoadFromAssemblyPath(typeof(TypeCacheProviderDiscoveryMetadataMutationTests).Assembly.Location);

            Resolve(resolver, new RootAssembly(), referenceName).Should().Be(path, "a referrer outside any load context binds through the default context");
            Resolve(resolver, customContextReferrer, referenceName).Should().BeNull();
        }
        finally
        {
            context.Unload();
        }
    }

    public void UnmarkedIntermediateAssemblyIsWalkedThroughItsMetadataAndOnlyTheMarkedOneIsLoaded()
    {
        List<(string Format, object?[] Arguments)> infos = CaptureInfos();
        List<(string Format, object?[] Arguments)> warnings = CaptureWarnings();
        using var directory = new TemporaryDirectory();
        string suffix = Guid.NewGuid().ToString("N");
        string marked = "Coverage.ChainMarked." + suffix;
        string unmarked = "Coverage.ChainUnmarked." + suffix;
        string markedPath = directory.WriteAssembly(marked, [], [(false, MarkerNamespace, MarkerName)]);
        string unmarkedPath = directory.WriteAssembly(unmarked, [marked], []);
        using var trustedAssemblies = new TrustedPlatformAssembliesOverride(markedPath + Path.PathSeparator + unmarkedPath);
        var root = new RootAssembly(AssemblyName.GetAssemblyName(unmarkedPath));

        List<Assembly> discovered = EnumerateCandidateAssemblies(root);

        discovered.Should().ContainSingle().Which.Should().BeSameAs(root);
        infos.Should().BeEmpty("both assemblies have a metadata path");
        warnings.Should().ContainSingle();
        warnings[0].Format.Should().Be("TypeCache: Could not load referenced assembly {0} for AssemblyFixtureProvider discovery. {1}");
        warnings[0].Arguments[0].Should().Be(AssemblyName.GetAssemblyName(markedPath).FullName);
        warnings[0].Arguments[1].Should().BeOfType<FileNotFoundException>();
    }
#endif

    private static (bool Result, bool HasMarker, AssemblyName[] References) TryReadProviderMetadata(string path)
    {
        MethodInfo tryReadProviderMetadata = typeof(TypeCache).GetMethod("TryReadProviderMetadata", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [path, false, null];

        bool result = (bool)tryReadProviderMetadata.Invoke(null, arguments)!;

        return (result, (bool)arguments[1]!, (AssemblyName[])arguments[2]!);
    }

    private static byte[] BuildAssembly(string name, string[] references, (bool IsTypeDefinition, string Namespace, string Name)[] attributes)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default, default);
        AssemblyDefinitionHandle assembly = metadata.AddAssembly(
            metadata.GetOrAddString(name),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        foreach (string reference in references)
        {
            metadata.AddAssemblyReference(metadata.GetOrAddString(reference), new Version(1, 0, 0, 0), default, default, 0, default);
        }

        byte[] signatureBytes = [0x20, 0x00, 0x01];
        byte[] valueBytes = [0x01, 0x00, 0x00, 0x00];
        BlobHandle constructorSignature = metadata.GetOrAddBlob(signatureBytes);
        BlobHandle attributeValue = metadata.GetOrAddBlob(valueBytes);
        AssemblyReferenceHandle? frameworkReference = null;
        int methodCount = 0;
        foreach ((bool isTypeDefinition, string attributeNamespace, string attributeName) in attributes)
        {
            EntityHandle constructor;
            if (isTypeDefinition)
            {
                metadata.AddTypeDefinition(
                    TypeAttributes.Public | TypeAttributes.Class,
                    metadata.GetOrAddString(attributeNamespace),
                    metadata.GetOrAddString(attributeName),
                    default,
                    MetadataTokens.FieldDefinitionHandle(1),
                    MetadataTokens.MethodDefinitionHandle(methodCount + 1));
                constructor = metadata.AddMethodDefinition(
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    MethodImplAttributes.IL,
                    metadata.GetOrAddString(".ctor"),
                    constructorSignature,
                    -1,
                    MetadataTokens.ParameterHandle(1));
                methodCount++;
            }
            else
            {
                frameworkReference ??= metadata.AddAssemblyReference(
                    metadata.GetOrAddString("Microsoft.VisualStudio.TestPlatform.TestFramework"),
                    new Version(1, 0, 0, 0),
                    default,
                    default,
                    0,
                    default);
                TypeReferenceHandle type = metadata.AddTypeReference(
                    frameworkReference.Value,
                    metadata.GetOrAddString(attributeNamespace),
                    metadata.GetOrAddString(attributeName));
                constructor = metadata.AddMemberReference(type, metadata.GetOrAddString(".ctor"), constructorSignature);
            }

            metadata.AddCustomAttribute(assembly, constructor, attributeValue);
        }

        var peBuilder = new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder(), flags: CorFlags.ILOnly);
        var peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);

        return peBlob.ToArray();
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
    private static object CreateResolver()
    {
        Type resolverType = typeof(TypeCache).GetNestedType("ProviderDiscoveryMetadataResolver", BindingFlags.NonPublic)!;

        return Activator.CreateInstance(resolverType, nonPublic: true)!;
    }

    private static string? Resolve(object resolver, Assembly referrer, AssemblyName referenceName)
    {
        MethodInfo resolve = resolver.GetType().GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance)!;
        object?[] arguments = [referrer, referenceName];

        return (string?)resolve.Invoke(resolver, arguments);
    }

    private static List<Assembly> EnumerateCandidateAssemblies(Assembly assembly)
    {
        MethodInfo enumerateCandidateAssemblies = typeof(TypeCache).GetMethod("EnumerateCandidateAssemblies", BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] arguments = [assembly];

        return ((IEnumerable<Assembly>)enumerateCandidateAssemblies.Invoke(null, arguments)!).ToList();
    }

    private List<(string Format, object?[] Arguments)> CaptureInfos()
    {
        List<(string Format, object?[] Arguments)> infos = [];
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(value => value.IsInfoEnabled).Returns(true);
        _testablePlatformServiceProvider.MockTraceLogger.Setup(value => value.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => infos.Add((format, arguments)));

        return infos;
    }

    private sealed class TrustedPlatformAssembliesOverride : IDisposable
    {
        private const string Key = "TRUSTED_PLATFORM_ASSEMBLIES";

        private readonly object? _original = AppContext.GetData(Key);

        public TrustedPlatformAssembliesOverride(string value) => AppContext.SetData(Key, value);

        public void Dispose() => AppContext.SetData(Key, _original);
    }

    private sealed class CollectibleLoadContext : AssemblyLoadContext
    {
        public CollectibleLoadContext()
            : base(isCollectible: true)
        {
        }
    }

    private sealed class RootAssembly(params AssemblyName[] references) : Assembly
    {
        public override string FullName => "Coverage.Root, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

        public override AssemblyName[] GetReferencedAssemblies() => references;
    }
#endif

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "ProviderMetadata-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string WriteAssembly(string name, string[] references, (bool IsTypeDefinition, string Namespace, string Name)[] attributes)
        {
            string path = Path.Combine(DirectoryPath, name + ".dll");
            File.WriteAllBytes(path, BuildAssembly(name, references, attributes));

            return path;
        }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
