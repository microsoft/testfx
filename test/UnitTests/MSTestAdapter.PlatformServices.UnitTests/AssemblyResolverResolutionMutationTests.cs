// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET && !WINDOWS_UWP

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests;

public sealed class AssemblyResolverResolutionMutationTests : TestContainer
{
    private const string ResolvingFormat = "MSTest.AssemblyResolver.OnResolve: Resolving assembly '{0}'";
    private const string PolicyFormat = "MSTest.AssemblyResolver.OnResolve: Resolving assembly after applying policy '{0}'";
    private const string SearchingFormat = "MSTest.AssemblyResolver.OnResolve: Searching assembly '{0}' in the directory '{1}'";
    private const string ResolvedAssemblyFormat = "MSTest.AssemblyResolver.OnResolve: Resolved assembly '{0}'";
    private const string ResolvedFromCacheFormat = "MSTest.AssemblyResolver.OnResolve: Resolved '{0}'";
    private const string FailedToLoadFormat = "MSTest.AssemblyResolver.OnResolve: Failed to load assembly '{0}'. Reason: {1}";

    private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
    private readonly TestablePlatformServiceProvider _provider = new();
    private readonly ConcurrentQueue<string> _entries = new();

    public AssemblyResolverResolutionMutationTests()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsWarningEnabled).Returns(true);
        _provider.MockTraceLogger.SetupGet(logger => logger.IsVerboseEnabled).Returns(true);
        _provider.MockTraceLogger.Setup(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _entries.Enqueue(Render("Info", format, arguments)));
        _provider.MockTraceLogger.Setup(logger => logger.Warning(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _entries.Enqueue(Render("Warning", format, arguments)));
        _provider.MockTraceLogger.Setup(logger => logger.Verbose(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback<string, object?[]>((format, arguments) => _entries.Enqueue(Render("Verbose", format, arguments)));
        PlatformServiceProvider.Instance = _provider;
    }

    protected override void Dispose(bool disposing)
    {
        PlatformServiceProvider.Instance = _previousProvider;
        base.Dispose(disposing);
    }

    public void ResolvingAnUnknownAssemblyLogsEveryStepAndRemembersTheFailure()
    {
        using ResolverDirectory directory = new();
        string name = NewMissingName();
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeNull();
        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeNull();

        EntriesMentioning(name).Should().Equal(
            Entry("Info", ResolvingFormat, name),
            Entry("Info", PolicyFormat, name),
            Entry("Verbose", SearchingFormat, name, directory.Root),
            Entry("Info", FailedToLoadFormat, name, nameof(FileNotFoundException)),
            Entry("Info", ResolvingFormat, name),
            Entry("Info", PolicyFormat, name),
            Entry("Info", ResolvedFromCacheFormat, name));
    }

    public void ResolvingDoesNotLogInformationWhenInfoLoggingIsDisabled()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        using ResolverDirectory directory = new();
        string name = NewMissingName();
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);

        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeNull();
        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeNull();

        EntriesMentioning(name).Should().Equal(
            [Entry("Verbose", SearchingFormat, name, directory.Root)],
            "only the verbose trace is enabled, so the resolution itself must have run without any information trace");
    }

    public void ReflectionOnlyResolutionIsServedFromItsOwnCache()
    {
        using ResolverDirectory directory = new();
        string name = NewMissingName();
        Assembly cached = typeof(AssemblyResolverResolutionMutationTests).Assembly;
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        GetCache(resolver, "_reflectionOnlyResolvedAssemblies")[name] = cached;

        InvokeOnResolveInternal(resolver, name, isReflectionOnly: true).Should().BeSameAs(cached);

        EntriesMentioning(name).Should().Equal(
            Entry("Info", ResolvingFormat, name),
            Entry("Info", PolicyFormat, name),
            Entry("Info", ResolvedFromCacheFormat, name));
        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeNull("the normal resolution cache does not know about the reflection-only entry");
    }

    public void AssembliesFoundInRunSettingsDirectoriesAreCachedAndTheirDirectoriesRemembered()
    {
        using ResolverDirectory directory = new();
        string runSettingsDirectory = Path.Combine(directory.Root, "runsettings");
        string childDirectory = Path.Combine(runSettingsDirectory, "child");
        Directory.CreateDirectory(childDirectory);
        Assembly testAssembly = typeof(AssemblyResolverResolutionMutationTests).Assembly;
        string found = "P14.Incremental." + Guid.NewGuid().ToString("N");
        File.Copy(testAssembly.Location, Path.Combine(runSettingsDirectory, found + ".dll"));
        string missing = NewMissingName();
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        resolver.AddSearchDirectoriesFromRunSetting([new RecursiveDirectoryPath(runSettingsDirectory, includeSubDirectories: true)]);

        resolver.OnResolve(null, new ResolveEventArgs(found)).Should().BeSameAs(testAssembly);

        EntriesMentioning(found).Should().Equal(
            Entry("Info", ResolvingFormat, found),
            Entry("Info", PolicyFormat, found),
            Entry("Verbose", SearchingFormat, found, directory.Root),
            Entry("Verbose", SearchingFormat, found, runSettingsDirectory),
            Entry("Info", ResolvedAssemblyFormat, found));

        resolver.OnResolve(null, new ResolveEventArgs(found)).Should().BeSameAs(testAssembly, "the assembly found in the run settings directory is cached");
        EntriesMentioning(found).Skip(5).Should().Equal(
            Entry("Info", ResolvingFormat, found),
            Entry("Info", PolicyFormat, found),
            Entry("Info", ResolvedFromCacheFormat, found));

        resolver.OnResolve(null, new ResolveEventArgs(missing)).Should().BeNull();

        EntriesMentioning(missing).Where(entry => entry.StartsWith("Verbose:", StringComparison.Ordinal)).Should().Equal(
            Entry("Verbose", SearchingFormat, missing, directory.Root),
            Entry("Verbose", SearchingFormat, missing, runSettingsDirectory),
            Entry("Verbose", SearchingFormat, missing, childDirectory));
    }

    public void TraceLoggingFailuresAreReportedOnStandardErrorOnlyOncePerResolver()
    {
        using ResolverDirectory directory = new();
        Assembly coreLibrary = typeof(string).Assembly;
        string name = coreLibrary.FullName!;
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Throws(new InvalidOperationException("boom"));
        _provider.MockTraceLogger.SetupGet(logger => logger.IsVerboseEnabled).Throws(new InvalidOperationException("boom"));
        using AssemblyResolver resolver = CreateDetachedResolver(directory.Root);
        using StandardErrorCapture capture = new();

        // The resolution logs three times (resolving, policy and search), so every one of those traces fails.
        resolver.OnResolve(null, new ResolveEventArgs(name)).Should().BeSameAs(coreLibrary);

        string output = capture.Output.ToString();
        output.Should().StartWith($"[MSTest.AssemblyResolver] Trace logging failed while resolving '{name}'. Exception: System.InvalidOperationException: boom");
        Regex.Count(output, Regex.Escape("[MSTest.AssemblyResolver]")).Should().Be(1, "only the first trace logging failure of a resolver is reported");
    }

    public void StandardErrorWriterFlushesEveryWrite()
    {
        MethodInfo createWriter = typeof(AssemblyResolver).GetMethod("CreateStandardErrorWriter", BindingFlags.Static | BindingFlags.NonPublic)!;

        var writer = (TextWriter)createWriter.Invoke(null, null)!;

        FindStreamWriter(writer).AutoFlush.Should().BeTrue();
    }

    private static AssemblyResolver CreateDetachedResolver(params string[] directories)
    {
        // Without its subscription, the resolver only sees what the test hands to it: the fallback load of an unknown
        // assembly does not re-enter the resolver.
        var resolver = new AssemblyResolver(directories);
        AppDomain.CurrentDomain.AssemblyResolve -= resolver.OnResolve;
        return resolver;
    }

    private static string NewMissingName()
        => "TestFx.P14.Missing." + Guid.NewGuid().ToString("N");

    private static Dictionary<string, Assembly?> GetCache(AssemblyResolver resolver, string fieldName)
        => (Dictionary<string, Assembly?>)typeof(AssemblyResolver).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resolver)!;

    private static Assembly? InvokeOnResolveInternal(AssemblyResolver resolver, string name, bool isReflectionOnly)
        => (Assembly?)typeof(AssemblyResolver)
            .GetMethod("OnResolveInternal", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(resolver, [null, new ResolveEventArgs(name), isReflectionOnly]);

    private static StreamWriter FindStreamWriter(TextWriter writer)
    {
        TextWriter? current = writer;
        while (current is not null and not StreamWriter)
        {
            FieldInfo? inner = null;
            for (Type? type = current.GetType(); type is not null && inner is null; type = type.BaseType)
            {
                inner = type
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(field => typeof(TextWriter).IsAssignableFrom(field.FieldType));
            }

            current = inner?.GetValue(current) as TextWriter;
        }

        return current as StreamWriter ?? throw new InvalidOperationException("The standard error writer does not wrap a StreamWriter anymore, this test has to follow the new layout.");
    }

    private static string Entry(string level, string format, params string[] arguments)
        => Render(level, format, arguments);

    private static string Render(string level, string format, IEnumerable<object?> arguments)
        => $"{level}: {format} [{string.Join(", ", arguments.Select(argument => argument is Exception exception ? exception.GetType().Name : argument?.ToString()))}]";

    private string[] EntriesMentioning(string text)
        => [.. _entries.Where(entry => entry.Contains(text, StringComparison.Ordinal))];

    private sealed class StandardErrorCapture : IDisposable
    {
        private static readonly FieldInfo StateField = GetLazyField("_state");
        private static readonly FieldInfo ValueField = GetLazyField("_value");

        private readonly Lazy<TextWriter> _lazyWriter;
        private readonly object? _originalState;
        private readonly object? _originalValue;

        public StandardErrorCapture()
        {
            _lazyWriter = (Lazy<TextWriter>)typeof(AssemblyResolver).GetField("StandardErrorWriter", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            _originalState = StateField.GetValue(_lazyWriter);
            _originalValue = ValueField.GetValue(_lazyWriter);

            // A lazy without state is already created: its value is returned as is.
            ValueField.SetValue(_lazyWriter, Output);
            StateField.SetValue(_lazyWriter, null);
        }

        public StringWriter Output { get; } = new();

        public void Dispose()
        {
            ValueField.SetValue(_lazyWriter, _originalValue);
            StateField.SetValue(_lazyWriter, _originalState);
        }

        private static FieldInfo GetLazyField(string name)
            => typeof(Lazy<TextWriter>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Lazy<T> does not have a '{name}' field anymore, this test has to follow the new layout.");
    }

    private sealed class ResolverDirectory : IDisposable
    {
        public ResolverDirectory() => Directory.CreateDirectory(Root);

        public string Root { get; } = Path.Combine(Path.GetTempPath(), "testfx-p14-resolver-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
