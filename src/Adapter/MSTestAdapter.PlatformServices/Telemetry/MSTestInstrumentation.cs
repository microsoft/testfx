// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// A distributed-tracing span opened by the MSTest engine.
/// </summary>
internal interface IMSTestActivity : IDisposable
{
    /// <summary>
    /// Adds an attribute to the span.
    /// </summary>
    void SetTag(string key, object? value);

    /// <summary>
    /// Marks the span as failed without recording an exception event.
    /// </summary>
    void SetFailed(string? description);

    /// <summary>
    /// Records an exception on the span and marks it as failed.
    /// </summary>
    void RecordException(Exception exception);
}

/// <summary>
/// The MSTest engine's tracing seam.
/// </summary>
/// <remarks>
/// <para>
/// The engine (this assembly) is deliberately free of any Microsoft.Testing.Platform or
/// <c>System.Diagnostics.DiagnosticSource</c> dependency: it also runs under VSTest and under target frameworks where
/// those are not available. So instead of creating activities itself, it asks a factory that the hosting adapter
/// installs at start-up (see <c>MSTestTestFramework</c>, which forwards to the platform's OpenTelemetry service).
/// </para>
/// <para>
/// When nothing installs a factory - which is the case for VSTest, or when the OpenTelemetry extension is not
/// registered - <see cref="StartActivity"/> returns <see langword="null"/> and every call site degrades to a null
/// check, so the cost is a single static field read.
/// </para>
/// <para>
/// This is what makes the *inside* of a test observable: without it a trace shows one span per test case and no
/// explanation for a slow test, whereas with it you can see that, say, <c>AssemblyInitialize</c> took 8 of the 9
/// seconds, or that a fixture and not the test body threw.
/// </para>
/// <para>
/// <b>Spans created here are never ambient.</b> MSTest captures the <see cref="System.Threading.ExecutionContext"/>
/// after <c>AssemblyInitialize</c>, <c>ClassInitialize</c> and the test-level fixtures so that async-locals set there
/// flow to every subsequent test (see <c>TestMethodRunner.ExecuteTestAsync</c>). The ambient activity is itself an
/// async-local, so an ambient span would be captured into that context and restored for the rest of the run -
/// parenting every later span, and any activity the user's own code starts, to a span that has long since ended.
/// The factory therefore creates spans that are timed and exported but never published as the current activity, and
/// parents them explicitly instead.
/// </para>
/// </remarks>
internal static class MSTestInstrumentation
{
    /// <summary>
    /// Span names, kept here so the tracing vocabulary is defined in one place.
    /// </summary>
    internal static class ActivityNames
    {
        internal const string AssemblyInitialize = "MSTest.AssemblyInitialize";
        internal const string AssemblyCleanup = "MSTest.AssemblyCleanup";
        internal const string ClassInitialize = "MSTest.ClassInitialize";
        internal const string ClassCleanup = "MSTest.ClassCleanup";
        internal const string TestInitialize = "MSTest.TestInitialize";
        internal const string TestCleanup = "MSTest.TestCleanup";
        internal const string TestMethod = "MSTest.TestMethod";
        internal const string Discovery = "MSTest.Discovery";
    }

    /// <summary>
    /// Attribute names emitted by the MSTest engine, on top of the platform's <c>test.*</c> conventions.
    /// </summary>
    internal static class Attributes
    {
        internal const string FixtureKind = "test.fixture.kind";
        internal const string TestClass = "test.suite.name";
        internal const string TestMethod = "test.case.name";
        internal const string TestAssembly = "test.assembly.name";
        internal const string RetryAttempt = "test.case.retry.attempt";
        internal const string DataRowIndex = "test.case.data_row.index";
        internal const string TimeoutMilliseconds = "test.case.timeout";
    }

    private static volatile Func<string, IEnumerable<KeyValuePair<string, object?>>?, IMSTestActivity?>? s_activityFactory;

    /// <summary>
    /// Gets a value indicating whether a tracing factory has been installed. Call sites should use this to avoid
    /// building tag payloads when tracing is off.
    /// </summary>
    internal static bool IsEnabled => s_activityFactory is not null;

    /// <summary>
    /// Installs (or, with <see langword="null"/>, removes) the factory used to create spans.
    /// </summary>
    internal static void SetActivityFactory(Func<string, IEnumerable<KeyValuePair<string, object?>>?, IMSTestActivity?>? activityFactory)
        => s_activityFactory = activityFactory;

    /// <summary>
    /// Starts a span, or returns <see langword="null"/> when tracing is not configured.
    /// </summary>
    internal static IMSTestActivity? StartActivity(string name, IEnumerable<KeyValuePair<string, object?>>? tags = null)
        => s_activityFactory?.Invoke(name, tags);

    /// <summary>
    /// Starts a span describing a fixture method (assembly/class/test initialize or cleanup).
    /// </summary>
    internal static IMSTestActivity? StartFixtureActivity(string name, string fixtureKind, string? owningType, string? assemblyName = null)
    {
        // Read the volatile field once: the tag array must not be built when tracing is off.
        Func<string, IEnumerable<KeyValuePair<string, object?>>?, IMSTestActivity?>? factory = s_activityFactory;
        return factory is null
            ? null
            : factory(
                name,
                [
                    new(Attributes.FixtureKind, fixtureKind),
                    new(Attributes.TestClass, owningType),
                    new(Attributes.TestAssembly, assemblyName),
                ]);
    }
}
