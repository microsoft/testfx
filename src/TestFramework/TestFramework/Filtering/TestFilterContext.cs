// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Read-only snapshot of the metadata MSTest exposes to an <see cref="ITestFilter"/> for a single
/// test under consideration.
/// </summary>
/// <remarks>
/// Only metadata that is available without loading the test type is exposed; <see cref="ITestFilter"/>
/// must be able to decide using strings, categories, traits, and priority alone. This is what allows
/// the filter to drop tests <em>before</em> their declaring type is loaded and before
/// <c>[AssemblyInitialize]</c> / <c>[ClassInitialize]</c> run.
/// </remarks>
public sealed class TestFilterContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestFilterContext"/> class.
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified test name (<c>Namespace.Class.Method</c>).</param>
    /// <param name="displayName">The display name reported for the test (often equal to <paramref name="testMethodName"/>).</param>
    /// <param name="testClassName">The fully qualified name of the declaring test class.</param>
    /// <param name="testMethodName">The unqualified test method name.</param>
    /// <param name="categories">The <see cref="TestCategoryAttribute"/> values declared on the test (and its class).</param>
    /// <param name="traits">The traits attached to the test. Multiple traits can share the same key.</param>
    /// <param name="priority">The <see cref="PriorityAttribute"/> value if any, otherwise <see langword="null"/>.</param>
    public TestFilterContext(
        string fullyQualifiedName,
        string displayName,
        string testClassName,
        string testMethodName,
        IReadOnlyList<string> categories,
        IReadOnlyList<KeyValuePair<string, string?>> traits,
        int? priority)
    {
        FullyQualifiedName = fullyQualifiedName ?? throw new ArgumentNullException(nameof(fullyQualifiedName));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        TestClassName = testClassName ?? throw new ArgumentNullException(nameof(testClassName));
        TestMethodName = testMethodName ?? throw new ArgumentNullException(nameof(testMethodName));
        Categories = categories ?? throw new ArgumentNullException(nameof(categories));
        Traits = traits ?? throw new ArgumentNullException(nameof(traits));
        Priority = priority;
    }

    /// <summary>Gets the fully qualified test name (<c>Namespace.Class.Method</c>).</summary>
    public string FullyQualifiedName { get; }

    /// <summary>Gets the display name of the test.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the fully qualified name of the declaring test class.</summary>
    public string TestClassName { get; }

    /// <summary>Gets the unqualified test method name.</summary>
    public string TestMethodName { get; }

    /// <summary>Gets the test categories declared via <see cref="TestCategoryAttribute"/>.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>
    /// Gets the traits attached to this test. Multiple traits can share the same key; consumers
    /// must therefore not assume the collection behaves like a dictionary.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string?>> Traits { get; }

    /// <summary>Gets the <see cref="PriorityAttribute"/> value if any, otherwise <see langword="null"/>.</summary>
    public int? Priority { get; }
}
