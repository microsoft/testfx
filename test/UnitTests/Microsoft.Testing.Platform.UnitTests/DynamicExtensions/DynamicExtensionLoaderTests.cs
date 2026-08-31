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
    private readonly Mock<ITestApplicationModuleInfo> _moduleInfo = new(MockBehavior.Strict);
    private readonly FakeAssemblyLoader _assemblyLoader = new();
    private readonly Mock<ITestApplicationBuilder> _builder = new();
    private readonly RecordingConsole _console = new();
    private CommandLineParseResult _parseResult = CreateParseResult(enableDynamicExtensions: true);

    private static CommandLineParseResult CreateParseResult(bool enableDynamicExtensions, bool serverMode = false, string? listTests = null)
    {
        List<CommandLineParseOption> options = [];
        if (enableDynamicExtensions)
        {
            options.Add(new CommandLineParseOption(PlatformCommandLineProvider.EnableDynamicExtensionsOptionKey, []));
        }

        if (serverMode)
        {
            options.Add(new CommandLineParseOption(PlatformCommandLineProvider.ServerOptionKey, []));
        }

        if (listTests is not null)
        {
            options.Add(new CommandLineParseOption(PlatformCommandLineProvider.DiscoverTestsOptionKey, [listTests]));
        }

        return new CommandLineParseResult(null, options, []);
    }

    [TestInitialize]
    public void Initialize()
    {
        RecordingHook.Reset();
        SecondRecordingHook.Reset();
        ThrowingHook.Reset();
        BaseHook.Reset();
        AsyncVoidHook.Reset();
        _moduleInfo.Setup(x => x.GetCurrentTestApplicationDirectory()).Returns(ApplicationDirectory);
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
    public async Task LoadAsync_WhenTheApplicationDirectoryIsMissing_TreatsItAsNothingDeclared()
    {
        // A genuinely absent directory declares nothing. This is distinguished from an *unreadable* one, which
        // throws (below): Directory.Exists cannot tell those apart, which is why it is not used as a pre-check.
        _fileSystem
            .Setup(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly))
            .Throws(new DirectoryNotFoundException());

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task DiscoverManifests_LooksInTheApplicationDirectoryNotTheWorkingDirectory()
    {
        // The single security-relevant property of this feature. The application directory is a fully trusted
        // application folder, but the working directory is where users keep data rather than code, so
        // discovering there would silently treat data as instructions -- see the .NET baseline security
        // assumptions, sections 2.1 and 3.1. A manifest placed in the working directory must be ignored, and
        // one in the application directory must be found, even when the two differ.
        string workingDirectory = Directory.GetCurrentDirectory();
        Assert.AreNotEqual(ApplicationDirectory, workingDirectory, StringComparer.OrdinalIgnoreCase, "The test is meaningless unless the two directories differ.");

        _fileSystem
            .Setup(x => x.GetFiles(workingDirectory, It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Throws(new InvalidOperationException("Discovery must never read the working directory."));
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        _fileSystem.Verify(x => x.GetFiles(ApplicationDirectory, DynamicExtensionConstants.ManifestSearchPattern, SearchOption.TopDirectoryOnly), Times.Once);
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
    public async Task LoadAsync_WhenTheRuntimeCannotLoadAssemblies_ThrowsAnActionableError()
    {
        // Native AOT surfaces as PlatformNotSupportedException from the load itself. Detecting it that way
        // rather than pre-checking RuntimeFeature.IsDynamicCodeSupported matters: <PublishAot>true</PublishAot>
        // turns that switch off even for builds whose managed output runs normally and can load extensions.
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));
        _assemblyLoader.ThrowOnLoad = new PlatformNotSupportedException("no dynamic loading");

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(PlatformCommandLineProvider.EnableDynamicExtensionsOptionKey, ex.Message);
        Assert.Contains(DynamicExtensionConstants.EnabledPropertyName, ex.Message);
        Assert.AreEqual(0, RecordingHook.InvocationCount);
    }

    [TestMethod]
    public async Task LoadAsync_WithOnlyDisabledExtensions_NeverTouchesTheAssemblyLoader()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook), enabled: false));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        Assert.IsEmpty(_assemblyLoader.LoadedPaths);
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
    public async Task LoadAsync_WhenHookIsAsyncVoid_ThrowsRatherThanInvokingIt()
    {
        // 'async void' passes the return-type check but behaves like the Task-returning hook that check
        // rejects: Invoke would return at the first await, so registrations would race the application's setup.
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(AsyncVoidHook)));

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains(nameof(AsyncVoidHook), ex.Message);
        Assert.IsFalse(AsyncVoidHook.WasInvoked, "A hook that cannot run to completion synchronously must not be invoked at all.");
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
        Assert.Contains(PlatformCommandLineProvider.EnableDynamicExtensionsOptionKey, ex.Message);
    }

    private static TestApplicationBuilder CreateRealBuilder()
        => new(
            new ApplicationLoggingState(LogLevel.None, new CommandLineParseResult(null, [], [])),
            DateTimeOffset.UtcNow,
            new TestApplicationOptions(),
            new Mock<IUnhandledExceptionsHandler>().Object,
            Args);

    [TestMethod]
    public async Task LoadAsync_WithoutTheOptIn_DoesNothingAtAll()
    {
        // Default-off is a predictability decision, not a security control (the application directory is
        // already fully trusted): a manifest that happens to be in an output directory must not silently
        // change how a run behaves. Nothing is read, parsed or loaded until the run asks for it.
        _parseResult = CreateParseResult(enableDynamicExtensions: false);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(0, RecordingHook.InvocationCount);
        _fileSystem.Verify(x => x.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
    }

    [TestMethod]
    public async Task LoadAsync_ReportsWhatItLoadedOnTheConsole()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        string output = _console.Output;

        // Running foreign code inside the test process must never be silent, and the diagnostic log is opt-in.
        Assert.Contains(typeof(RecordingHook).FullName!, output);
        Assert.Contains("a.testingplatformextensions.json", output);
        Assert.Contains("TestExtension.dll", output);
    }

    [TestMethod]
    public async Task LoadAsync_InServerMode_KeepsStandardOutputClean()
    {
        // Server mode owns stdout as a protocol channel; writing the notice there would corrupt the stream.
        _parseResult = CreateParseResult(enableDynamicExtensions: true, serverMode: true);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount, "The extension must still load in server mode.");
        Assert.IsEmpty(_console.Output);
    }

    [TestMethod]
    public async Task LoadAsync_InListTestsJsonMode_KeepsStandardOutputClean()
    {
        // stdout is reserved for a single JSON document in this mode -- even the platform banner is
        // suppressed -- so a notice here would produce output no JSON parser can read.
        _parseResult = CreateParseResult(enableDynamicExtensions: true, listTests: PlatformCommandLineProvider.DiscoverTestsJsonArgument);
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.AreEqual(1, RecordingHook.InvocationCount, "The extension must still load when discovering tests.");
        Assert.IsEmpty(_console.Output);
    }

    [TestMethod]
    public async Task LoadAsync_InListTestsTextMode_StillReportsWhatItLoaded()
    {
        // Only the json argument reserves stdout; the human-readable listing must keep the notice.
        _parseResult = CreateParseResult(enableDynamicExtensions: true, listTests: "text");
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.Contains(typeof(RecordingHook).FullName!, _console.Output);
    }

    [TestMethod]
    public async Task LoadAsync_WhenNothingIsLoaded_WritesNothingToTheConsole()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook), enabled: false));

        await CreateLoader().LoadAsync(_builder.Object, Args);

        Assert.IsEmpty(_console.Output);
    }

    [TestMethod]
    public async Task LoadAsync_WhenALaterExtensionFails_StillReportsTheOnesThatAlreadyRan()
    {
        // By the time a later hook fails the earlier ones have already run and changed the application, so
        // staying silent about them would hide that exactly when something has gone wrong.
        SetupManifest(
            "a.testingplatformextensions.json",
            Wrap(ManifestEntryFor(typeof(RecordingHook)) + "," + ManifestEntryFor(typeof(ThrowingHook))));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.AreEqual(1, RecordingHook.InvocationCount);
        Assert.Contains(typeof(RecordingHook).FullName!, _console.Output);
        Assert.DoesNotContain(typeof(ThrowingHook).FullName!, _console.Output, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task LoadAsync_WhenTheFirstExtensionFails_WritesNothingToTheConsole()
    {
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(ThrowingHook)));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.IsEmpty(_console.Output);
    }

    [TestMethod]
    public async Task LoadAsync_WhenReportingAlsoFailsDuringAFailure_KeepsTheActionableError()
    {
        // Reporting runs while unwinding a load failure. It only writes when something did load, so the case
        // that matters is a partial load: one hook succeeded, a later one threw, and stdout is broken too (a
        // closed pipe, say). The console exception must not replace the InvalidOperationException naming the
        // manifest and the extension -- that message is the only part the user can act on.
        SetupManifest(
            "a.testingplatformextensions.json",
            Wrap(ManifestEntryFor(typeof(RecordingHook)) + "," + ManifestEntryFor(typeof(ThrowingHook))));
        _console.ThrowOnWrite = new IOException("stdout is closed");

        InvalidOperationException ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));

        Assert.Contains("a.testingplatformextensions.json", ex.Message);
    }

    [TestMethod]
    public async Task LoadAsync_WhenReportingFailsOnTheSuccessPath_Throws()
    {
        // Nothing else is going wrong here, so swallowing the failure would leave extensions loaded with no
        // notice at all, which is exactly what the reporting exists to prevent.
        SetupManifest("a.testingplatformextensions.json", ManifestFor(typeof(RecordingHook)));
        _console.ThrowOnWrite = new IOException("stdout is closed");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => CreateLoader().LoadAsync(_builder.Object, Args));
    }

    private DynamicExtensionLoader CreateLoader()
        => new(_fileSystem.Object, _moduleInfo.Object, _assemblyLoader, _console, _parseResult, logger: null);

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
    /// Captures what the loader writes to standard output. <see cref="IConsole"/> is wide and only the
    /// line-writing members matter here, so the rest are left unimplemented rather than mocked.
    /// </summary>
    private sealed class RecordingConsole : IConsole
    {
        private readonly StringBuilder _output = new();

        /// <summary>
        /// Simulates a console whose underlying stream has failed, for example a closed stdout pipe.
        /// </summary>
        public Exception? ThrowOnWrite { get; set; }

        public string Output => _output.ToString();

        public void WriteLine() => _output.AppendLine();

        public void WriteLine(string? value)
        {
            if (ThrowOnWrite is { } ex)
            {
                throw ex;
            }

            _output.AppendLine(value);
        }

        public void Write(string? value) => _output.Append(value);

        public void Write(char value) => _output.Append(value);

        public void Write(StringBuilder value) => _output.Append(value);

        public int BufferHeight => throw new NotSupportedException();

        public int BufferWidth => throw new NotSupportedException();

        public int WindowHeight => throw new NotSupportedException();

        public int WindowWidth => throw new NotSupportedException();

        public bool IsOutputRedirected => throw new NotSupportedException();

        public ConsoleColor GetForegroundColor() => throw new NotSupportedException();

        public void SetForegroundColor(ConsoleColor color) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public event ConsoleCancelEventHandler? CancelKeyPress
        {
            add => throw new NotSupportedException();
            remove => throw new NotSupportedException();
        }
    }

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

    public static class AsyncVoidHook
    {
        public static bool WasInvoked { get; private set; }

        // Deliberately 'async void': this fixture exists to prove the loader rejects that shape, which is
        // exactly the crash-the-process hazard VSTHRD100 warns about. It is never invoked.
#pragma warning disable VSTHRD100 // Avoid "async void" methods
        public static async void AddExtensions(ITestApplicationBuilder builder, string[] args)
#pragma warning restore VSTHRD100
        {
            _ = builder;
            _ = args;
            WasInvoked = true;
            await Task.Yield();
        }

        public static void Reset() => WasInvoked = false;
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
