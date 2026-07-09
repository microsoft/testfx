// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

/// <summary>
/// The unit test element.
/// </summary>
#if NETFRAMEWORK
[Serializable]
#endif
[DebuggerDisplay("{TestMethod.DisplayName} ({TestMethod.ManagedTypeName})")]
internal sealed class UnitTestElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestElement"/> class.
    /// </summary>
    /// <param name="testMethod"> The test method. </param>
    /// <exception cref="ArgumentNullException"> Thrown when method is null. </exception>
    public UnitTestElement(TestMethod testMethod)
    {
        if (testMethod is null)
        {
            throw new ArgumentNullException(nameof(testMethod));
        }

        DebugEx.Assert(testMethod.FullClassName != null, "Full className cannot be empty");
        TestMethod = testMethod;
    }

    /// <summary>
    /// Gets the test method which should be executed as part of this test case.
    /// </summary>
    public TestMethod TestMethod { get; private set; }

    public TestDataSourceUnfoldingStrategy UnfoldingStrategy { get; set; } = TestDataSourceUnfoldingStrategy.Auto;

    /// <summary>
    /// Gets or sets the test categories for test method.
    /// </summary>
    public string[]? TestCategory { get; set; }

    /// <summary>
    /// Gets or sets the traits for test method.
    /// </summary>
    public TestTrait[]? Traits { get; set; }

    /// <summary>
    /// Gets or sets the priority of the test method, if any.
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this test method should not execute in parallel.
    /// </summary>
    public bool DoNotParallelize { get; set; }

#if !WINDOWS_UWP && !WIN_UI
    /// <summary>
    /// Gets or sets the deployment items for the test method.
    /// </summary>
    public KeyValuePair<string, string>[]? DeploymentItems { get; set; }
#endif

    internal string? DeclaringFilePath { get; set; }

    internal int? DeclaringLineNumber { get; set; }

    /// <summary>
    /// Gets or sets the Work Item Ids for the test method.
    /// </summary>
    internal string[]? WorkItemIds { get; set; }

    /// <summary>
    /// Gets or sets host-provided execution context properties (for example the values historically read
    /// from a test-case-management host: run id, plan id, build configuration, test point id, ...) that are
    /// surfaced to the running test through <c>TestContext</c>. Keyed by the host property identifier so the
    /// platform services layer does not depend on a specific test platform's property object model. This is
    /// only populated when a host supplies such values (for internally discovered tests it stays null).
    /// </summary>
#if NETFRAMEWORK
    // Consumed on the source-processing side before execution; the isolation host copy never reads it.
    [field: NonSerialized]
#endif
    internal IReadOnlyDictionary<string, object?>? ExecutionContextProperties { get; set; }

    /// <summary>
    /// Gets or sets the host's test case for this test, used to report its lifecycle and results back to the
    /// host with full fidelity (preserving any host-injected data — such as test-case-management or
    /// data-collector properties — that the neutral model does not otherwise carry) and to describe it to the
    /// (still VSTest-based) deployment service. For tests handed to the adapter by a host it is that original
    /// test case; for tests discovered internally by the platform services it is <see langword="null"/> until
    /// materialized on demand by the adapter's <c>UnitTestElementExtensions.GetOrCreateHostTestCase</c>. The execution engine treats it as an
    /// opaque handle — it reads nothing VSTest-specific off it and only threads it into the result recorder and
    /// the deployment boundary.
    /// </summary>
#if NETFRAMEWORK
    // Result recording and deployment happen on the source-processing side; the isolation host copy never reads it.
    [field: NonSerialized]
#endif
    internal object? HostRecordingHandle { get; set; }

    /// <summary>
    /// Gets or sets the cached <see cref="Guid"/> test node UID for the native Microsoft.Testing.Platform path.
    /// Populated lazily by <c>UnitTestElementExtensions.GetTestId</c> and cleared by any clone operation that
    /// changes the assembly source (<see cref="CloneWithSource"/>, <see cref="CloneWithUpdatedSource"/>).
    /// </summary>
    internal Guid? CachedTestNodeUid { get; set; }

    internal UnitTestElement Clone()
    {
        var clone = (UnitTestElement)MemberwiseClone();
        clone.TestMethod = TestMethod.Clone();
        return clone;
    }

    // Legacy source-updating clone used only by the ToTestCase / test-case filter bridge
    // (TestCaseExtensions.ToUnitTestElementWithUpdatedSource). It delegates to the buggy
    // TestMethod.CloneWithUpdatedSource and is retained to preserve that path's exact current behavior; the
    // execution engine uses WithUpdatedSource / CloneWithSource instead. Tracked by
    // https://github.com/microsoft/testfx/issues/9573.
    internal UnitTestElement CloneWithUpdatedSource(string source)
    {
        var clone = (UnitTestElement)MemberwiseClone();
        clone.TestMethod = TestMethod.CloneWithUpdatedSource(source);
        clone.CachedTestNodeUid = null;
        return clone;
    }

    // Correct source-updating clone: the returned clone (only) targets the new source. Used by the execution
    // engine via WithUpdatedSource. See https://github.com/microsoft/testfx/issues/9573.
    internal UnitTestElement CloneWithSource(string source)
    {
        var clone = (UnitTestElement)MemberwiseClone();
        clone.TestMethod = TestMethod.CloneWithSource(source);
        clone.CachedTestNodeUid = null;
        return clone;
    }

    /// <summary>
    /// Returns this element when it already targets <paramref name="source"/>, otherwise a clone whose test
    /// method points at <paramref name="source"/>. This mirrors the source resolution the adapter previously
    /// performed while converting a host test case, so a deployed source (relocated to the deployment
    /// directory) is honored without reloading the assembly from its original location (see
    /// https://github.com/microsoft/testfx/issues/6713).
    /// </summary>
    /// <param name="source">The (possibly deployment-relocated) source of the test.</param>
    /// <returns>An element whose <see cref="ObjectModel.TestMethod.AssemblyName"/> is <paramref name="source"/>.</returns>
    internal UnitTestElement WithUpdatedSource(string source)
        => TestMethod.AssemblyName == source ? this : CloneWithSource(source);
}
