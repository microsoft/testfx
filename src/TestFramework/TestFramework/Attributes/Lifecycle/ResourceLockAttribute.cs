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
/// A lock is acquired before its scheduling chunk starts and released after the chunk finishes, so the
/// chunk - not the individual test method - determines how long a lock is held. What forms a chunk
/// depends on <see cref="ParallelizeAttribute.Scope"/>:
/// </para>
/// <para>
/// Under <see cref="ExecutionScope.ClassLevel"/> (the default) the chunk is the entire class, so a
/// class-level lock is taken once and held across every test in the class, spanning
/// <c>[ClassInitialize]</c> and <c>[ClassCleanup]</c>. The chunk's locks are the union of the class's
/// and every method's declared keys, each upgraded to the strongest mode declared anywhere in the
/// class - so under this scope declaring locks on individual methods does not make locking more
/// granular.
/// </para>
/// <para>
/// Under <see cref="ExecutionScope.MethodLevel"/> the chunk is a single test, so a class-level lock is
/// applied to each test individually and acquired and released per test. Tests from other classes may
/// therefore interleave between two tests of this class, and a resource established in
/// <c>[ClassInitialize]</c> is not continuously owned through <c>[ClassCleanup]</c>.
/// </para>
/// <para>
/// In both cases a lock covering a test is held across that test's <c>[TestInitialize]</c> and
/// <c>[TestCleanup]</c>. When a chunk holds more than one lock, the locks are acquired in
/// ordinal-sorted key order, which makes deadlock impossible.
/// </para>
/// <para>
/// If a test is also marked <see cref="DoNotParallelizeAttribute"/>, that attribute takes precedence
/// and the declared resource locks have no effect: such tests run in the sequential phase and never
/// pass through the parallel scheduler that acquires locks.
/// </para>
/// <para>
/// This attribute is inherited: a lock declared on a base test class applies to every class deriving
/// from it, matching <see cref="DoNotParallelizeAttribute"/>. This is the conservative direction -
/// a base fixture that touches a shared resource still touches it from derived classes, and
/// over-locking merely runs slower while under-locking produces races. Note the consequence: a
/// derived class cannot remove an inherited lock, nor weaken an inherited
/// <see cref="ResourceAccessMode.ReadWrite"/> to <see cref="ResourceAccessMode.Read"/>, so under
/// <see cref="ExecutionScope.ClassLevel"/> a lock on a widely-used base class serializes the whole
/// hierarchy. Declare locks on the most derived type that actually needs them.
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
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="resource"/> is empty or whitespace.</exception>
    public ResourceLockAttribute(string resource)
    {
        if (resource is null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        // An empty or whitespace key is never intentional, and because conflict detection is plain string
        // equality it would silently become a shared key that serializes unrelated tests against each other.
        if (resource.Trim().Length == 0)
        {
            throw new ArgumentException(FrameworkMessages.InvalidResourceLockResource, nameof(resource));
        }

        Resource = resource;
    }

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
