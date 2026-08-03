// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.DynamicExtensions;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[DoNotParallelize] // The hook types below record their invocations in static state.
public sealed class DynamicExtensionLoaderTests
{
    private const string ApplicationDirectory = "/app";
    private static readonly string[] Args = ["--some-option"];

    private readonly Mock<IFileSystem> _fileSystem = new(MockBehavior.Strict);
    private readonly Mock<IEnvironment> _environment = new(MockBehavior.Strict);
    private readonly Mock<IRuntimeFeature> _runtimeFeature = new(MockBehavior.Strict);
    private readonly Mock<ITestApplicationModuleInfo> _moduleInfo = new(MockBehavior.Strict);
    private readonly FakeAssemblyLoader _assemblyLoader = new();
    private readonly Mock<ITestApplicationBuilder> _builder = new();

    [TestInitialize]
    public void Initialize()
    {
        RecordingHook.Reset();
        SecondRecordingHook.Reset();
        ThrowingHook.Reset();
        BaseHook.Reset();

        _environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS)).Returns((string?)null);
        _runtimeFeature.SetupGet(x => x.IsDynamicCodeSupported).Returns(true);
        _moduleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(ApplicationDirectory);
        _fileSystem.Setup(x => x.ExistDirectory(ApplicationDirectory)).Returns(true);
    }

    [TestMethod]
    public async Task LoadAsync_WithNoManifest_DoesNothing()
    {
        SetupManifests();

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        Assert.IsEmpty(_assemblyLoader.LoadedPaths);
    }

    [TestMethod]
    public async Task LoadAsync_WithoutManifest_DoesNotEnumerateWhenDirectoryIsMissing()
    {
        _fileSystem.Setup(x => x.ExistDirectory(ApplicationDirectory)).Returns(false);

        await CreateLoader().LoadAsync(_builder.Object, Args);

        _fileSystem.Verify(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [TestMethod]
    public async Task LoadAsync_WithEnabledExtension_InvokesTheHookWithTheBuilderAndArgs()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        Assert.AreSame(_builder.Object, RecordingHook.LastBuilder);
        Assert.AreSame(Args, RecordingHook.LastArgs);
    }

    [TestMethod]
    public async Task LoadAsync_WhenDisabledByEnvironmentVariable_SkipsDiscoveryEntirely()
    {
        _environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS)).Returns("1");

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        _fileSystem.Verify(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("true")]
    public async Task LoadAsync_KillSwitchAcceptsDocumentedValues(string value)
    {
        _environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS)).Returns(value);

        await CreateLoader().LoadAsync(_builder.Object, Args);

        _fileSystem.Verify(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("false")]
    [DataRow("")]
    public async Task LoadAsync_KillSwitchIgnoresOtherValues(string value)
    {
        _environment.Setup(x => x.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS)).Returns(value);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WithDisabledExtension_DoesNotLoadTheAssembly()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook), enabled: false));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        Assert.IsEmpty(_assemblyLoader.LoadedPaths);
    }

    [TestMethod]
    public async Task LoadAsync_WithSameIdInTwoManifests_RegistersOnce()
    {
        string entry = ManifestEntryFor(typeof(RecordingHook), id: "shared-id");
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(entry)),
            ("b.testingplatformextensions.json", Wrap(entry)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_IdComparisonIsCaseInsensitive()
    {
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook), id: "Shared-Id"))),
            ("b.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook), id: "shared-id"))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WithoutExplicitId_DeduplicatesOnPathAndType()
    {
        string entry = ManifestEntryFor(typeof(RecordingHook));
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(entry)),
            ("b.testingplatformextensions.json", Wrap(entry)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WithDistinctExtensions_RegistersAllOfThem()
    {
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook)))),
            ("b.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(SecondRecordingHook)))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        Assert.AreEqual(1, SecondRecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_RegistersManifestsInFileNameOrderRegardlessOfEnumerationOrder()
    {
        // The file system returns 'z' first; ordering must not depend on that.
        SetupManifests(
            ("z.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(SecondRecordingHook)))),
            ("a.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook)))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreSequenceEqual([nameof(RecordingHook), nameof(SecondRecordingHook)], InvocationLog.Snapshot());
    }

    [TestMethod]
    public async Task LoadAsync_RegistersEntriesOfOneManifestInDeclarationOrder()
    {
        SetupManifest(
            "a.testingplatformextensions.json",
            Wrap(ManifestEntryFor(typeof(SecondRecordingHook)) + "," + ManifestEntryFor(typeof(RecordingHook))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreSequenceEqual([nameof(SecondRecordingHook), nameof(RecordingHook)], InvocationLog.Snapshot());
    }

    [TestMethod]
    public async Task LoadAsync_IgnoresFilesThatOnlyLookLikeAManifest()
    {
        // Directory search patterns can over-match on Windows; the loader must re-filter on the exact suffix.
        string extraneous = Path.Combine(ApplicationDirectory, "a.testingplatformextensions.jsonbackup");
        _fileSystem
            .Setup(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly))
            .Returns([extraneous]);

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        _fileSystem.Verify(x => x.ReadAllTextAsync(extraneous), Times.Never);
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssemblyIsMissing_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)), assemblyExists: false);

        FileNotFoundException ex = await Assert.ThrowsExactlyAsync<FileNotFoundException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains("a.testingplatformextensions.json", ex.Message);
        Assert.IsEmpty(_assemblyLoader.LoadedPaths);
    }

    [TestMethod]
    public async Task LoadAsync_WhenTypeIsMissing_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", Wrap(ManifestEntry("Contoso.Missing.Hook")));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains("Contoso.Missing.Hook", ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookMethodIsMissing_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(HookWithoutAddExtensions)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(HookWithoutAddExtensions), ex.Message);
        Assert.Contains(DynamicExtensionConstants.HookMethodName, ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookMethodHasWrongSignature_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(HookWithWrongSignature)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(HookWithWrongSignature), ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookIsNotStatic_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(HookWithInstanceMethod)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(HookWithInstanceMethod), ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookThrows_WrapsTheOriginalException()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(ThrowingHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(ThrowingHook), ex.Message);
        Assert.IsInstanceOfType<NotSupportedException>(ex.InnerException, "The extension's own exception must be preserved as the inner exception.");
        Assert.AreEqual(ThrowingHook.Message, ex.InnerException!.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenAssemblyLoadFails_WrapsTheOriginalException()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));
        _assemblyLoader.ThrowOnLoad = new BadImageFormatException("boom");

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.IsInstanceOfType<BadImageFormatException>(ex.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_WithoutDynamicCodeSupport_ThrowsBeforeLoadingAnything()
    {
        _runtimeFeature.SetupGet(x => x.IsDynamicCodeSupported).Returns(false);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS, ex.Message);
        Assert.IsEmpty(_assemblyLoader.LoadedPaths);
    }

    [TestMethod]
    public async Task LoadAsync_WithoutDynamicCodeSupportAndOnlyDisabledExtensions_DoesNotThrow()
    {
        _runtimeFeature.SetupGet(x => x.IsDynamicCodeSupported).Returns(false);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook), enabled: false));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WhenManifestCannotBeRead_Throws()
    {
        string manifestPath = Path.Combine(ApplicationDirectory, "a.testingplatformextensions.json");
        _fileSystem
            .Setup(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly))
            .Returns([manifestPath]);
        _fileSystem.Setup(x => x.ReadAllTextAsync(manifestPath)).ThrowsAsync(new IOException("locked"));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(manifestPath, ex.Message);
        Assert.IsInstanceOfType<IOException>(ex.InnerException);
    }

    [TestMethod]
    public async Task LoadAsync_TwoEntriesInTheSameAssembly_AreBothRegistered()
    {
        SetupManifest(
            "a.testingplatformextensions.json",
            Wrap(ManifestEntryFor(typeof(RecordingHook)) + "," + ManifestEntryFor(typeof(SecondRecordingHook))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        Assert.AreEqual(1, SecondRecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookReturnsTask_ThrowsRatherThanSilentlyNotAwaiting()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(AsyncHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(AsyncHook), ex.Message);
        Assert.IsFalse(AsyncHook.WasInvoked, "A hook that cannot be awaited must not be invoked at all.");
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookReturnsValue_Throws()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(NonVoidHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(NonVoidHook), ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenHookIsInheritedFromABaseType_IsFound()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(DerivedHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, BaseHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WhenTheSameIdDeclaresDifferentExtensions_Throws()
    {
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook), id: "shared-id"))),
            ("b.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(SecondRecordingHook), id: "shared-id"))));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains("shared-id", ex.Message);
        Assert.Contains("a.testingplatformextensions.json", ex.Message);
        Assert.Contains("b.testingplatformextensions.json", ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_ADisabledEntryNeitherCollidesWithNorBlocksAnEnabledOneReusingItsId()
    {
        // 'enabled: false' is the per-extension escape hatch, so a switched-off declaration must not be able to
        // hard-fail the run over an id it shares with something that does load. Nothing is silently dropped
        // here: the author of the disabled entry explicitly asked for it not to be deployed.
        SetupManifests(
            ("a.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(SecondRecordingHook), id: "shared-id", enabled: false))),
            ("b.testingplatformextensions.json", Wrap(ManifestEntryFor(typeof(RecordingHook), id: "shared-id"))));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        Assert.AreEqual(0, SecondRecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_HookCannotRegisterATestFramework()
    {
        // Uses the real builder: the guard lives inside it, precisely so that hooks keep receiving the genuine
        // ITestApplicationBuilder that shipped helpers downcast.
        TestApplicationBuilder realBuilder = CreateRealBuilder();
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(TestFrameworkRegisteringHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(realBuilder, Args));

        Assert.Contains("a.testingplatformextensions.json", ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_TestFrameworkCanStillBeRegisteredAfterTheHooksRan()
    {
        // The guard must be scoped to the hook invocation, not latched for the rest of the process.
        TestApplicationBuilder realBuilder = CreateRealBuilder();
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(realBuilder, Args);

        realBuilder.RegisterTestFramework(
            _ => new Mock<ITestFrameworkCapabilities>().Object,
            (_, _) => new Mock<ITestFramework>().Object);
    }

    [TestMethod]
    public async Task LoadAsync_HookReceivesTheRealBuilderSoDowncastingHelpersKeepWorking()
    {
        TestApplicationBuilder realBuilder = CreateRealBuilder();
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(realBuilder, Args);

        Assert.AreSame(realBuilder, RecordingHook.LastBuilder);
    }

    [TestMethod]
    public async Task LoadAsync_HookCanStillReachTheRealManagers()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(ManagerTouchingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        _builder.VerifyGet(x => x.CommandLine, Times.AtLeastOnce);
        _builder.VerifyGet(x => x.TestHost, Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task LoadAsync_WhenManifestDirectoryCannotBeRead_Throws()
    {
        _fileSystem
            .Setup(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly))
            .Throws(new UnauthorizedAccessException("denied"));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(ApplicationDirectory, ex.Message);
        Assert.Contains(EnvironmentVariableConstants.TESTINGPLATFORM_NODYNAMICEXTENSIONS, ex.Message);
    }

    private static TestApplicationBuilder CreateRealBuilder()
        => new(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            new Mock<IUnhandledExceptionsHandler>().Object,
            Args);

    private DynamicExtensionLoader CreateLoader()
        => new(_fileSystem.Object, _environment.Object, _runtimeFeature.Object, _moduleInfo.Object, _assemblyLoader, logger: null);

    private void SetupManifest(string fileName, string content, bool assemblyExists = true)
        => SetupManifests(assemblyExists, (fileName, content));

    private void SetupManifests(params (string FileName, string Content)[] manifests)
        => SetupManifests(assemblyExists: true, manifests);

    private void SetupManifests(bool assemblyExists, params (string FileName, string Content)[] manifests)
    {
        string[] paths = [.. manifests.Select(m => Path.Combine(ApplicationDirectory, m.FileName))];
        _fileSystem
            .Setup(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly))
            .Returns(paths);

        for (int i = 0; i < manifests.Length; i++)
        {
            string content = manifests[i].Content;
            _fileSystem.Setup(x => x.ReadAllTextAsync(paths[i])).ReturnsAsync(content);
        }

        _fileSystem.Setup(x => x.ExistFile(It.IsAny<string>())).Returns(assemblyExists);
    }

    private static string ManifestFor(Type hookType, bool enabled = true)
        => Wrap(ManifestEntryFor(hookType, enabled: enabled));

    private static string ManifestEntryFor(Type hookType, string? id = null, bool enabled = true)
        => ManifestEntry(hookType.FullName!, id, enabled);

    private static string ManifestEntry(string typeFullName, string? id = null, bool enabled = true)
    {
        string idProperty = id is null ? string.Empty : $"\"id\": \"{id}\", ";
        return $$"""{ {{idProperty}}"assemblyPath": "TestExtension.dll", "typeFullName": "{{typeFullName}}", "enabled": {{(enabled ? "true" : "false")}} }""";
    }

    private static string Wrap(string entries) => $$"""{ "extensions": [ {{entries}} ] }""";

    /// <summary>
    /// Returns the unit test assembly for every path, so the hook types declared below can be resolved.
    /// </summary>
    private sealed class FakeAssemblyLoader : IDynamicExtensionAssemblyLoader
    {
        public List<string> LoadedPaths { get; } = [];

        public Exception? ThrowOnLoad { get; set; }

        public bool IsIsolated => true;

        public Assembly LoadAssembly(string assemblyPath)
        {
            LoadedPaths.Add(assemblyPath);
            return ThrowOnLoad is null ? typeof(FakeAssemblyLoader).Assembly : throw ThrowOnLoad;
        }
    }

    private static class InvocationLog
    {
        private static readonly List<string> Entries = [];

        public static void Add(string name)
        {
            lock (Entries)
            {
                Entries.Add(name);
            }
        }

        public static void Clear()
        {
            lock (Entries)
            {
                Entries.Clear();
            }
        }

        public static string[] Snapshot()
        {
            lock (Entries)
            {
                return [.. Entries];
            }
        }
    }

    public static class RecordingHook
    {
        public static int InvocationCount { get; private set; }

        public static ITestApplicationBuilder? LastBuilder { get; private set; }

        public static string[]? LastArgs { get; private set; }

        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            InvocationCount++;
            LastBuilder = builder;
            LastArgs = args;
            InvocationLog.Add(nameof(RecordingHook));
        }

        public static void Reset()
        {
            InvocationCount = 0;
            LastBuilder = null;
            LastArgs = null;
            InvocationLog.Clear();
        }
    }

    public static class SecondRecordingHook
    {
        public static int InvocationCount { get; private set; }

        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
            InvocationCount++;
            InvocationLog.Add(nameof(SecondRecordingHook));
        }

        public static void Reset() => InvocationCount = 0;
    }

    public static class ThrowingHook
    {
        public const string Message = "The extension refused to register.";

        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
            throw new NotSupportedException(Message);
        }

        public static void Reset()
        {
        }
    }

    public static class HookWithoutAddExtensions
    {
        public static void SomethingElse(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
        }
    }

    public static class HookWithWrongSignature
    {
        public static void AddExtensions(ITestApplicationBuilder builder)
            => _ = builder;
    }

    public sealed class HookWithInstanceMethod
    {
        public void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
        }
    }

    public static class AsyncHook
    {
        public static bool WasInvoked { get; private set; }

        public static Task AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
            WasInvoked = true;
            return Task.CompletedTask;
        }
    }

    public static class NonVoidHook
    {
        public static int AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
            return 0;
        }
    }

    public class BaseHook
    {
        public static int InvocationCount { get; private set; }

        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = builder;
            _ = args;
            InvocationCount++;
        }

        public static void Reset() => InvocationCount = 0;
    }

    public sealed class DerivedHook : BaseHook;

    public static class TestFrameworkRegisteringHook
    {
        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = args;
            builder.RegisterTestFramework(
                _ => throw new NotSupportedException(),
                (_, _) => throw new NotSupportedException());
        }
    }

    public static class ManagerTouchingHook
    {
        public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
        {
            _ = args;
            _ = builder.CommandLine;
            _ = builder.TestHost;
        }
    }
}
