// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Declares that a test (or every test of a test class) must not start until one or more other tests
/// have finished, forming a dependency graph that the in-assembly scheduler executes in topological
/// order. Unlike a flat ordering attribute, independent branches of the graph still run in parallel.
/// </summary>
/// <remarks>
/// <para>
/// The attribute declares an edge "this test depends on that test". Applying it several times is how
/// a test declares more than one prerequisite (fan-in); several tests naming the same prerequisite is
/// how a graph fans out. Fan-out is the point of the feature: once the shared prerequisite has passed,
/// every dependent becomes runnable at the same time and the scheduler is free to run them
/// concurrently, subject to <see cref="ParallelizeAttribute"/> and worker availability.
/// </para>
/// <para>
/// When applied to a class, the dependency applies to every test in that class. When the target is a
/// <em>type</em> (rather than a specific method), the edge points at <em>every</em> test of that type,
/// so the dependent starts only once all of them have finished.
/// </para>
/// <para>
/// <strong>Scope.</strong> Dependencies are resolved within a single test source. A target in another
/// assembly cannot be waited on, even though <see cref="DependsOnAttribute(Type)"/> and
/// <see cref="DependsOnAttribute(Type, string)"/> will happily accept a type from one: such an edge
/// matches nothing and is reported as an ignored dependency rather than silently ordering anything.
/// </para>
/// <para>
/// <strong>Failure semantics.</strong> If a prerequisite does not pass, the dependent is
/// <em>skipped</em>, not failed, and the skip propagates transitively down the graph. Skipping is the
/// established convention (TestNG's <c>dependsOnMethods</c>, TUnit's <c>[DependsOn]</c>,
/// pytest-dependency) and it keeps the signal readable: one root cause is reported as one failure plus
/// a set of clearly-labelled skips, instead of a wall of failures. Set
/// <see cref="ProceedOnFailure"/> to <see langword="true"/> for a dependent that must run anyway --
/// typically an audit or cleanup test.
/// </para>
/// <para>
/// <strong>Cycles.</strong> A dependency cycle is a configuration error and is reported before any
/// test in the assembly runs. Every test in the cycle is failed with a message naming the cycle path.
/// </para>
/// <para>
/// <strong>Interaction with parallelization.</strong> Ordering is enforced regardless of
/// <see cref="ParallelizeAttribute.Scope"/>. Under <see cref="ExecutionScope.ClassLevel"/> (the
/// default) the scheduling unit is the whole class, so dependencies between two classes are honored at
/// class granularity; a dependency between two <em>methods of the same class</em> orders them within
/// that class's run. Under <see cref="ExecutionScope.MethodLevel"/> every test is scheduled
/// individually, which gives the most parallelism and the finest ordering. If class-level scheduling
/// would require running two classes before each other (class A's test depends on class B's and vice
/// versa), the declared order is still honored - those classes' tests are run sequentially, in
/// dependency order, and a warning names them - but they lose their parallelism; switching to
/// <see cref="ExecutionScope.MethodLevel"/> gets it back.
/// </para>
/// <para>
/// A test marked <see cref="DoNotParallelizeAttribute"/> runs in the sequential phase, which happens
/// after the parallel phase. A parallelizable test that depends on such a test is therefore moved into
/// the sequential phase as well (transitively), so that its prerequisite really has run first.
/// </para>
/// <para>
/// <strong>Data-driven tests.</strong> Naming a test that expands into several test cases (for example
/// via <c>[DataRow]</c>) creates an edge to <em>all</em> of its cases: the dependent waits for every
/// row and is skipped if any row does not pass. Per-row matching is not supported.
/// </para>
/// <para>
/// <strong>Inheritance.</strong> A test method declared on a base class runs as a test of every derived
/// test class, and the dependency it declares travels with it: the edge is resolved against the
/// <em>derived</em> class, so each derived class gets its own edge between its own copies of the two
/// tests. Dropping the edge there would silently discard the declared ordering in every concrete test
/// class. What <c>Inherited = false</c> opts out of is <em>override</em> chains: a method that overrides a
/// dependent test without re-declaring the attribute has no dependency, because re-pointing a prerequisite
/// onto a method the author rewrote tends to create edges nobody asked for.
/// </para>
/// <para>
/// Test dependencies couple tests together and make it impossible to run a dependent in isolation, so
/// they are a poor fit for unit tests. They exist for multi-step integration and end-to-end suites,
/// where re-establishing expensive state in every test is impractical.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [TestClass]
/// public class CheckoutTests
/// {
///     [TestMethod]
///     public void CreateCart() { }
///
///     // Fan-out: both of these wait for CreateCart, then may run in parallel with each other.
///     [TestMethod]
///     [DependsOn(nameof(CreateCart))]
///     public void AddItem() { }
///
///     [TestMethod]
///     [DependsOn(nameof(CreateCart))]
///     public void ApplyCoupon() { }
///
///     // Fan-in: waits for both.
///     [TestMethod]
///     [DependsOn(nameof(AddItem))]
///     [DependsOn(nameof(ApplyCoupon))]
///     public void Checkout() { }
///
///     // Runs even when its prerequisite failed.
///     [TestMethod]
///     [DependsOn(nameof(Checkout), ProceedOnFailure = true)]
///     public void WriteAuditRecord() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class DependsOnAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class declaring a dependency
    /// on another test method of the same test class.
    /// </summary>
    /// <param name="testMethodName">
    /// The name of the prerequisite test method. Use <c>nameof</c> so that renaming the method is
    /// caught by the compiler.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="testMethodName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="testMethodName"/> is empty or whitespace.</exception>
    public DependsOnAttribute(string testMethodName)
    {
        if (testMethodName is null)
        {
            throw new ArgumentNullException(nameof(testMethodName));
        }

        if (testMethodName.Trim().Length == 0)
        {
            throw new ArgumentException(FrameworkMessages.InvalidDependsOnTestMethodName, nameof(testMethodName));
        }

        TestMethodName = testMethodName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class declaring a dependency
    /// on <em>every</em> test of another test class.
    /// </summary>
    /// <param name="testClass">The prerequisite test class.</param>
    /// <exception cref="ArgumentNullException"><paramref name="testClass"/> is <see langword="null"/>.</exception>
    public DependsOnAttribute(Type testClass)
        => TestClass = testClass ?? throw new ArgumentNullException(nameof(testClass));

    /// <summary>
    /// Initializes a new instance of the <see cref="DependsOnAttribute"/> class declaring a dependency
    /// on a specific test method of another test class.
    /// </summary>
    /// <param name="testClass">The test class declaring the prerequisite.</param>
    /// <param name="testMethodName">
    /// The name of the prerequisite test method. Use <c>nameof</c> so that renaming the method is
    /// caught by the compiler.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="testClass"/> or <paramref name="testMethodName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="testMethodName"/> is empty or whitespace.</exception>
    public DependsOnAttribute(Type testClass, string testMethodName)
    {
        if (testMethodName is null)
        {
            throw new ArgumentNullException(nameof(testMethodName));
        }

        if (testMethodName.Trim().Length == 0)
        {
            throw new ArgumentException(FrameworkMessages.InvalidDependsOnTestMethodName, nameof(testMethodName));
        }

        TestClass = testClass ?? throw new ArgumentNullException(nameof(testClass));
        TestMethodName = testMethodName;
    }

    /// <summary>
    /// Gets the test class declaring the prerequisite, or <see langword="null"/> when the prerequisite
    /// is a method of the same class as the dependent.
    /// </summary>
    public Type? TestClass { get; }

    /// <summary>
    /// Gets the name of the prerequisite test method, or <see langword="null"/> when the dependency
    /// targets every test of <see cref="TestClass"/>.
    /// </summary>
    public string? TestMethodName { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the dependent still runs when a prerequisite does not
    /// pass. Defaults to <see langword="false"/>, meaning the dependent is skipped. Set it to
    /// <see langword="true"/> for a test that must run regardless of its prerequisites' outcome, such
    /// as an audit or cleanup test; ordering is still enforced.
    /// </summary>
    /// <remarks>
    /// The flag is evaluated per dependent test rather than per edge: a test that declares several
    /// prerequisites runs past a failure only when <em>every</em> one of its declarations sets this to
    /// <see langword="true"/>. One declaration left at the default is therefore enough to skip the test
    /// when any of its prerequisites does not pass. That conservative direction is deliberate - opting
    /// out of waiting on one prerequisite says nothing about the others, and running a test whose
    /// remaining preconditions were never established just produces a second, misleading failure.
    /// </remarks>
    public bool ProceedOnFailure { get; set; }
}
