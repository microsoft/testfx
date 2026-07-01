// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Snapshot of the metadata MSTest exposes to an <see cref="ITestFilter"/> for a single
/// test under consideration.
/// </summary>
/// <remarks>
/// <para>
/// Only metadata that is available without loading the test type is exposed; <see cref="ITestFilter"/>
/// must be able to decide using strings, categories, traits, and priority alone. This is what allows
/// the filter to drop tests <em>before</em> their declaring type is loaded and before
/// <c>[AssemblyInitialize]</c> / <c>[ClassInitialize]</c> run.
/// </para>
/// <para>
/// Properties are mutable (settable) so the platform can populate them via an object initializer
/// and so the type can grow new metadata fields over time without breaking existing
/// <see cref="ITestFilter"/> implementations or their unit tests. MSTest itself does not mutate
/// a <see cref="TestFilterContext"/> after it is handed to an <see cref="ITestFilter"/>; filters
/// should likewise treat the instance they receive as effectively read-only for the duration of
/// the call.
/// </para>
/// <para>
/// Each property describes a separate facet of the test. The flat string properties
/// (<see cref="FullyQualifiedName"/>, <see cref="DisplayName"/>) are convenience views and are always
/// populated. The structured properties (<see cref="Namespace"/>, <see cref="ClassName"/>,
/// <see cref="ManagedTypeName"/>, <see cref="ManagedMethodName"/>, <see cref="MethodArity"/>,
/// <see cref="ParameterTypeFullNames"/>) may be <see langword="null"/> when the metadata is not
/// available — typically for tests discovered through code paths that don't surface ECMA-335
/// managed names.
/// </para>
/// <para>
/// The type is designed to be extended over time: new properties can be added without breaking
/// existing <see cref="ITestFilter"/> implementations or their unit tests. Consumers construct
/// instances using an object initializer (e.g. <c>new TestFilterContext { FullyQualifiedName = "…" }</c>);
/// no positional constructor needs to be updated when new properties land.
/// </para>
/// </remarks>
[Experimental("MSTESTEXP", UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
public sealed class TestFilterContext
{
    /// <summary>
    /// Gets or sets the fully qualified test name in <c>Namespace.Class.Method</c> form.
    /// </summary>
    /// <remarks>
    /// This mirrors the historical VSTest <c>TestCase.FullyQualifiedName</c> shape and is intended
    /// for filters that want a single string to match against.
    /// </remarks>
    public string FullyQualifiedName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the test, as reported to the runner / IDE.
    /// </summary>
    /// <remarks>
    /// Often equal to <see cref="MethodName"/>; differs for data-driven tests, tests with a
    /// custom <see cref="TestMethodAttribute"/> display name, or attributes that override it.
    /// </remarks>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unqualified test method name (i.e. without class or namespace).
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the test assembly file containing this test.
    /// </summary>
    /// <remarks>
    /// Matches VSTest <c>TestCase.Source</c>: the absolute path to the <c>.dll</c> being run.
    /// This is <em>not</em> a simple assembly name; see <see cref="ManagedTypeName"/> if you
    /// need an ECMA-335-style identifier.
    /// </remarks>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the namespace of the declaring test class, or <see langword="null"/> when
    /// no managed metadata is available or the class is in the global namespace.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the simple class name (without namespace), or <see langword="null"/> when
    /// no managed metadata is available. Nested types are surfaced using their managed metadata
    /// form (e.g. <c>Outer+Inner</c>); see <see cref="ManagedTypeName"/> for the fully escaped
    /// ECMA-335 representation.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Gets or sets the declaring type name in ECMA-335 metadata form, or <see langword="null"/>
    /// when no managed metadata is available.
    /// </summary>
    /// <remarks>
    /// Includes the namespace, uses <c>+</c> for nested types, and uses backtick + arity for
    /// generics (e.g. <c>Acme.MyOuter+MyInner`1</c>). Matches the format defined by the
    /// <see href="https://github.com/microsoft/vstest/blob/main/RFCs/0017-Managed-TestCase-Properties.md">
    /// Managed TestCase Properties RFC</see>.
    /// </remarks>
    public string? ManagedTypeName { get; set; }

    /// <summary>
    /// Gets or sets the method name in ECMA-335 metadata form, or <see langword="null"/> when no
    /// managed metadata is available.
    /// </summary>
    /// <remarks>
    /// Includes the method name, generic arity suffix, and parameter type list
    /// (e.g. <c>MyMethod`1(System.Int32)</c>). Matches the format defined by the
    /// <see href="https://github.com/microsoft/vstest/blob/main/RFCs/0017-Managed-TestCase-Properties.md">
    /// Managed TestCase Properties RFC</see>.
    /// </remarks>
    public string? ManagedMethodName { get; set; }

    /// <summary>
    /// Gets or sets the number of generic type parameters declared on the test method,
    /// or <see langword="null"/> when no managed metadata is available. Zero indicates a
    /// non-generic method.
    /// </summary>
    public int? MethodArity { get; set; }

    /// <summary>
    /// Gets or sets the ECMA-335-style fully qualified parameter type names of the test method,
    /// or <see langword="null"/> when no managed metadata is available. An empty list indicates
    /// a parameterless method.
    /// </summary>
    public IReadOnlyList<string>? ParameterTypeFullNames { get; set; }

    /// <summary>
    /// Gets or sets the test categories declared via <see cref="TestCategoryAttribute"/> on the
    /// method or its declaring class. Defaults to an empty list.
    /// </summary>
    public IReadOnlyList<string> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the traits attached to this test. Multiple traits can share the same key;
    /// consumers must therefore not assume the collection behaves like a dictionary. Defaults
    /// to an empty list.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string?>> Traits { get; set; } = [];

    /// <summary>
    /// Gets or sets the <see cref="PriorityAttribute"/> value declared on the test, or
    /// <see langword="null"/> when no priority was declared.
    /// </summary>
    public int? Priority { get; set; }
}
