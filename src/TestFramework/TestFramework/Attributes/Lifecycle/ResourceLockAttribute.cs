// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Declares that a test or test class contends on a named shared resource, so that the in-assembly
/// parallel scheduler serializes it only against other tests that declare the same resource, instead
/// of forcing whole classes or assemblies to opt out of parallelization with
/// <see cref="DoNotParallelizeAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// The resource is identified by a free-form, opaque string key. Two locks refer to the same resource
/// if and only if their <see cref="Resource"/> strings are equal using an ordinal, case-sensitive
/// comparison. Keys are identifiers, not paths: no hierarchical relationship is inferred between them
/// (for example <c>C:\out</c> and <c>C:\out\sub</c> are unrelated keys). Prefer <c>const</c> fields
/// and <see cref="WellKnownResources"/> over inline string literals so that a typo becomes a compile
/// error rather than a silent race.
/// </para>
/// <para>
/// Locking scope is the current test host process only. This attribute does not coordinate across
/// processes or machines, so it does not make parallel <c>dotnet test</c> invocations mutually
/// exclusive.
/// </para>
/// <para>
/// A method-level lock is held across the whole test, spanning <c>[TestInitialize]</c> and
/// <c>[TestCleanup]</c>. A class-level lock is held across the whole class chunk, spanning
/// <c>[ClassInitialize]</c> and <c>[ClassCleanup]</c>, and applies to every test in the class. When a
/// test declares more than one lock, the locks are acquired in ordinal-sorted key order, which makes
/// deadlock impossible.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ResourceLockAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceLockAttribute"/> class for the specified
    /// resource, using <see cref="ResourceAccessMode.ReadWrite"/> access.
    /// </summary>
    /// <param name="resource">The opaque key identifying the shared resource.</param>
    public ResourceLockAttribute(string resource)
        => Resource = resource;

    /// <summary>
    /// Gets the opaque key identifying the shared resource. Compared using an ordinal, case-sensitive
    /// comparison.
    /// </summary>
    public string Resource { get; }

    /// <summary>
    /// Gets or sets the access mode for the resource. Defaults to
    /// <see cref="ResourceAccessMode.ReadWrite"/> (exclusive). Set to
    /// <see cref="ResourceAccessMode.Read"/> when the test only reads the resource, so that it can run
    /// concurrently with other readers and blocks only against a writer.
    /// </summary>
    public ResourceAccessMode Mode { get; set; } = ResourceAccessMode.ReadWrite;
}
